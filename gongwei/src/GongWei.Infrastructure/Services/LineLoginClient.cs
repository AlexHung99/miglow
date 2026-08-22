using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GongWei.Infrastructure.Services;

public sealed class LineLoginOptions
{
    /// <summary>LINE Login Channel ID. Public — it travels in the authorize URL.</summary>
    public string ChannelId { get; set; } = null!;

    /// <summary>
    /// Injected through user-secrets in development and the IIS configuration store in
    /// production. Never appsettings.json, never Git, never a log line (§11).
    /// </summary>
    public string ChannelSecret { get; set; } = null!;

    /// <summary>Must match the Console entry byte for byte, including the trailing path.</summary>
    public string RedirectUri { get; set; } = null!;

    public string AuthorizeEndpoint { get; set; } = "https://access.line.me/oauth2/v2.1/authorize";

    public string TokenEndpoint { get; set; } = "https://api.line.me/oauth2/v2.1/token";

    public string VerifyEndpoint { get; set; } = "https://api.line.me/oauth2/v2.1/verify";

    /// <summary>LINE's OIDC issuer. Fixed here so a compromised response cannot redefine it.</summary>
    public string Issuer { get; set; } = "https://access.line.me";

    /// <summary>NTP keeps the clock close; 60s covers the remaining drift (§4.3).</summary>
    public int ClockSkewSeconds { get; set; } = 60;

    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(ChannelId)
            || string.IsNullOrWhiteSpace(ChannelSecret)
            || string.IsNullOrWhiteSpace(RedirectUri))
        {
            throw new InvalidOperationException(
                "LineLogin:ChannelId, LineLogin:ChannelSecret and LineLogin:RedirectUri must all be " +
                "configured. Set the secret through user-secrets or the IIS configuration store.");
        }
    }
}

/// <summary>
/// The LINE half of the login (line_login_v1.1 §3.3, §4.2, §4.3).
///
/// Two things this class will not do. It never lets an upstream response body reach an
/// exception message or a log, because LINE echoes the request — including the channel
/// secret — in some error payloads. And it never decodes the ID token locally; the token
/// is verified by LINE's endpoint, which checks the signature this code cannot.
/// </summary>
public sealed class LineLoginClient(
    HttpClient http,
    IOptions<LineLoginOptions> options,
    ILogger<LineLoginClient> logger) : ILineLoginClient
{
    private readonly LineLoginOptions _options = options.Value;

    public Uri BuildAuthorizeUri(LineAuthorizeRequest request)
    {
        _options.EnsureConfigured();

        var query = new (string Key, string Value)[]
        {
            ("response_type", "code"),
            ("client_id", _options.ChannelId),
            ("redirect_uri", _options.RedirectUri),
            ("state", request.State),
            ("scope", "openid profile"),
            ("nonce", request.Nonce),
            ("code_challenge", request.CodeChallenge),
            ("code_challenge_method", "S256")
        };

        var encoded = string.Join('&', query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return new Uri($"{_options.AuthorizeEndpoint}?{encoded}");
    }

    public async Task<LineTokenSet> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken ct = default)
    {
        _options.EnsureConfigured();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ChannelId,
            ["client_secret"] = _options.ChannelSecret,
            ["code_verifier"] = codeVerifier
        };

        // An authorization code is single-use: retrying a 4xx would burn it for nothing.
        // Only transport failures and 5xx get the one retry the spec allows (§4.2).
        var response = await SendWithOneRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            },
            ct);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "LINE token exchange failed with status {StatusCode}.", (int)response.StatusCode);

                throw new DomainException(
                    ErrorCodes.AuthLineTokenFailed,
                    "LINE 登入暫時失敗。",
                    DomainErrorKind.Unauthenticated);
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);

            if (token is null || string.IsNullOrEmpty(token.IdToken))
            {
                // scope=openid was requested, so a missing id_token means the channel is
                // misconfigured rather than the player doing anything unusual.
                logger.LogError("LINE token response carried no id_token; check the channel's OpenID setting.");

                throw new DomainException(
                    ErrorCodes.AuthLineTokenFailed,
                    "LINE 登入暫時失敗。",
                    DomainErrorKind.Unauthenticated);
            }

            return new LineTokenSet(token.IdToken);
        }
    }

    public async Task<VerifiedLineIdentity> VerifyIdTokenAsync(
        string idToken,
        string nonce,
        CancellationToken ct = default)
    {
        _options.EnsureConfigured();

        // Sending the nonce makes LINE reject a token minted for a different attempt;
        // it is the replay defence, so it is not optional.
        var form = new Dictionary<string, string>
        {
            ["id_token"] = idToken,
            ["client_id"] = _options.ChannelId,
            ["nonce"] = nonce
        };

        var response = await SendWithOneRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, _options.VerifyEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            },
            ct);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "LINE ID token verification rejected with status {StatusCode}.",
                    (int)response.StatusCode);

                throw IdTokenInvalid();
            }

            var claims = await response.Content.ReadFromJsonAsync<VerifyResponse>(ct)
                         ?? throw IdTokenInvalid();

            Validate(claims, nonce);

            return new VerifiedLineIdentity(claims.Sub, claims.Name ?? "無名", claims.Picture);
        }
    }

    /// <summary>
    /// Re-checks everything the verify endpoint already checked. Belt and braces: if the
    /// endpoint is ever swapped for local JWT validation, these assertions stay behind.
    /// </summary>
    private void Validate(VerifyResponse claims, string expectedNonce)
    {
        if (!string.Equals(claims.Iss, _options.Issuer, StringComparison.Ordinal))
        {
            logger.LogWarning("LINE ID token carried an unexpected issuer.");
            throw IdTokenInvalid();
        }

        if (!string.Equals(claims.Aud, _options.ChannelId, StringComparison.Ordinal))
        {
            logger.LogWarning("LINE ID token was issued for a different channel.");
            throw IdTokenInvalid();
        }

        var now = DateTimeOffset.UtcNow;
        var skew = TimeSpan.FromSeconds(_options.ClockSkewSeconds);

        if (DateTimeOffset.FromUnixTimeSeconds(claims.Exp) + skew < now)
        {
            logger.LogWarning("LINE ID token had already expired.");
            throw IdTokenInvalid();
        }

        if (DateTimeOffset.FromUnixTimeSeconds(claims.Iat) - skew > now)
        {
            logger.LogWarning("LINE ID token was issued in the future.");
            throw IdTokenInvalid();
        }

        // Constant-time: a timing oracle on the nonce would hand an attacker the value
        // they need to replay a token (§4.3).
        if (claims.Nonce is null
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(claims.Nonce),
                Encoding.UTF8.GetBytes(expectedNonce)))
        {
            logger.LogWarning("LINE ID token nonce did not match the login attempt.");
            throw IdTokenInvalid();
        }

        if (string.IsNullOrWhiteSpace(claims.Sub) || claims.Sub.Length > 255)
        {
            logger.LogWarning("LINE ID token carried no usable subject.");
            throw IdTokenInvalid();
        }
    }

    private async Task<HttpResponseMessage> SendWithOneRetryAsync(
        Func<HttpRequestMessage> request,
        CancellationToken ct)
    {
        try
        {
            var first = await http.SendAsync(request(), ct);

            if ((int)first.StatusCode < 500)
            {
                return first;
            }

            first.Dispose();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("LINE endpoint unreachable, retrying once.");
        }

        await Task.Delay(TimeSpan.FromMilliseconds(250), ct);

        return await http.SendAsync(request(), ct);
    }

    private static DomainException IdTokenInvalid() => new(
        ErrorCodes.AuthIdTokenInvalid,
        "無法驗證登入身分。",
        DomainErrorKind.Unauthenticated);

    private sealed record TokenResponse(
        [property: JsonPropertyName("id_token")] string? IdToken);

    /// <summary>
    /// LINE's verify endpoint returns the decoded claim set. <c>aud</c> is a single
    /// string here, unlike the array some OIDC providers return.
    /// </summary>
    private sealed record VerifyResponse(
        [property: JsonPropertyName("iss")] string Iss,
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("aud")] string Aud,
        [property: JsonPropertyName("exp")] long Exp,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("nonce")] string? Nonce,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("picture")] string? Picture);
}
