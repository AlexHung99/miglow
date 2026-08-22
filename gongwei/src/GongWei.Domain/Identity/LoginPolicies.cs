using GongWei.Domain.Common;

namespace GongWei.Domain.Identity;

/// <summary>
/// Decides whether a caller-supplied return URL may be redirected to after login
/// (line_login_v1.1 §3.1).
///
/// This is the open-redirect boundary. It is an allowlist of exactly one origin and one
/// path prefix — never a "does it contain miglow.vip" test, because
/// <c>https://miglow.vip.evil.example/</c> and <c>https://evil.example/?x=miglow.vip</c>
/// both pass that kind of check.
/// </summary>
public static class ReturnUrlPolicy
{
    public const string AllowedHost = "miglow.vip";
    public const string AllowedPathPrefix = "/gongwei/";

    /// <summary>Used when the caller supplies nothing, and as the target for error redirects.</summary>
    public const string Default = "https://miglow.vip/gongwei/";

    /// <summary>Mirrors the varchar(500) column.</summary>
    public const int MaxLength = 500;

    /// <summary>
    /// Returns the URL to redirect to, or null if the candidate is not permitted.
    ///
    /// The returned value is rebuilt from the parsed components rather than echoed back,
    /// so anything the parser ignored cannot survive into the Location header.
    /// </summary>
    public static string? Normalise(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength)
        {
            return null;
        }

        // Control characters would let a crafted value split the Location header.
        if (candidate.Any(char.IsControl))
        {
            return null;
        }

        // Backslashes: some browsers historically treated "https:/\evil" as a host change.
        if (candidate.Contains('\\'))
        {
            return null;
        }

        // "//evil.example" is scheme-relative and would leave the site entirely.
        if (candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // Credentials in the authority ("https://miglow.vip@evil.example/") point the
        // browser at the host after the @, not at the one a human reads first.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        if (!uri.IsDefaultPort)
        {
            return null;
        }

        if (!string.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // AbsolutePath is already percent-decoded once by Uri. Decoding it again must not
        // change it, otherwise "%252e%252e%252f" would slip a traversal past this check.
        var path = uri.AbsolutePath;

        if (!string.Equals(Uri.UnescapeDataString(path), path, StringComparison.Ordinal))
        {
            return null;
        }

        if (!path.StartsWith(AllowedPathPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        // The hash route is the SPA's own state and never reaches the server on the way
        // back, but it has to be preserved so the player lands where they started.
        var rebuilt = $"https://{AllowedHost}{path}{uri.Query}{uri.Fragment}";

        return rebuilt.Length <= MaxLength ? rebuilt : null;
    }

    /// <summary>
    /// Builds the redirect used when login fails. Only a stable code travels back —
    /// never a LINE error description, exception text or anything else (§6).
    /// </summary>
    public static string ErrorRedirect(string? returnUrl, string errorCode)
    {
        var target = Normalise(returnUrl) ?? Default;

        // The SPA is a hash router: its own route lives after '#', so an error appended to
        // the query string would be invisible to it. Replace the fragment instead.
        var withoutFragment = target.Split('#')[0];

        return $"{withoutFragment}#/login-error?code={Uri.EscapeDataString(errorCode)}";
    }
}

/// <summary>Lifetimes and quotas for one LINE Login round trip (line_login_v1.1 §3.2).</summary>
public static class LoginAttemptPolicy
{
    /// <summary>An attempt is redeemable for ten minutes, then it is dead.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Consumed and expired rows are swept by the worker after this long.</summary>
    public static readonly TimeSpan RetentionAfterExpiry = TimeSpan.FromHours(24);

    /// <summary>256-bit state and nonce; 32 bytes before Base64Url encoding.</summary>
    public const int TokenByteCount = 32;

    /// <summary>PKCE verifier: 48 bytes encodes to 64 Base64Url characters, inside 43–128.</summary>
    public const int VerifierByteCount = 48;

    public const int StartsPerMinutePerIp = 20;
}

/// <summary>
/// Whether an account in a given state may be handed a session (line_login_v1.1 §4.4).
/// A dead character does not block login — the player has to sign in to apply again.
/// </summary>
public static class UserStatusPolicy
{
    public static string? RejectionCode(UserStatus status) => status switch
    {
        UserStatus.Active => null,
        UserStatus.Suspended => ErrorCodes.AuthAccountSuspended,
        // Reviving a deleted account automatically would defeat the retention policy.
        UserStatus.Deleted => ErrorCodes.AuthAccountSuspended,
        _ => ErrorCodes.AuthAccountSuspended
    };
}
