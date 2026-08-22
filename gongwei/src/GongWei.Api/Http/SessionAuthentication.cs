using System.Security.Claims;
using System.Text.Encodings.Web;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GongWei.Api.Http;

public sealed class SessionCookieOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Player cookie name. The admin site uses a different one (gw_admin_session) on a
    /// different host, so a stolen player cookie is useless there (spec §11).
    /// </summary>
    public string CookieName { get; set; } = "gw_session";
}

/// <summary>
/// Cookie-backed session authentication. The cookie holds an opaque token; the server
/// stores only its SHA-256 and can revoke it at any time (spec §11).
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<SessionCookieOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    SessionService sessions) : AuthenticationHandler<SessionCookieOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "GongWeiSession";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(Options.CookieName, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var principal = await sessions.ResolveAsync(token, isAdminSession: false, Context.RequestAborted);

        if (principal is null)
        {
            // Clear the dead cookie so the browser stops sending it on every request.
            Response.Cookies.Delete(Options.CookieName);
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(GongWeiClaims.UserId, principal.UserId.ToString()));
        identity.AddClaim(new Claim(GongWeiClaims.SessionId, principal.SessionId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, principal.DisplayName));

        foreach (var role in principal.AdminRoles)
        {
            identity.AddClaim(new Claim(GongWeiClaims.AdminRole, role.ToString()));
        }

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

/// <summary>
/// Session-bound CSRF check for every state-changing player request
/// (line_login_v1.1 §5.2).
///
/// Not double-submit: the token is compared against a secret hash held on the session
/// row, so a token minted for one session is worthless on another. There is no CSRF
/// cookie at all, which is why a subdomain that could set cookies on the parent domain
/// still cannot forge a request.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF-Token";

    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    /// <summary>
    /// The only exempt state-changing routes. Logout and logout-all are deliberately not
    /// here: forcing a player to log out is a real attack, small but real.
    /// </summary>
    private static readonly string[] ExemptPaths =
    [
        "/api/v1/auth/line/start",
        "/api/v1/auth/line/callback",
        "/api/v1/auth/csrf"
    ];

    public async Task InvokeAsync(HttpContext context, SessionService sessions)
    {
        if (SafeMethods.Contains(context.Request.Method)
            || ExemptPaths.Any(p => context.Request.Path.StartsWithSegments(p))
            || context.Request.Path.StartsWithSegments("/api/v1/health"))
        {
            await next(context);
            return;
        }

        var sessionIdClaim = context.User.FindFirst(GongWeiClaims.SessionId)?.Value;

        // No session, nothing to forge. Hand the request on so authorization answers 401
        // AUTH_REQUIRED and a bad path answers 404.
        //
        // Rejecting here instead made every unauthenticated POST/PATCH/DELETE return 403
        // — including ones whose route does not exist — so a caller could not tell "not
        // signed in" from "wrong token" from "no such endpoint". Nothing is lost: CSRF is
        // an attack on an authenticated session, and without one there is no authority to
        // borrow.
        if (!Guid.TryParse(sessionIdClaim, out var sessionId))
        {
            await next(context);
            return;
        }

        var headerToken = context.Request.Headers[HeaderName].ToString();

        var ok = !string.IsNullOrEmpty(headerToken)
                 && await sessions.ValidateCsrfAsync(sessionId, headerToken, context.RequestAborted);

        if (!ok)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json; charset=utf-8";

            await context.Response.WriteAsJsonAsync(new
            {
                type = "about:blank",
                title = ErrorCodes.CsrfInvalid,
                status = 403,
                detail = $"Missing or invalid {HeaderName}. Fetch a new token from GET /api/v1/auth/csrf.",
                code = ErrorCodes.CsrfInvalid,
                requestId = context.TraceIdentifier
            });

            return;
        }

        await next(context);
    }
}

/// <summary>Parsing helpers for the <c>If-Match</c> optimistic-concurrency header (spec §8.3).</summary>
public static class ETagHelper
{
    public static string Format(long version) => $"\"{version}\"";

    /// <summary>
    /// Reads a required If-Match. Absence is a 428-style error rather than a silent
    /// last-writer-wins, which is the whole point of the header.
    /// </summary>
    public static long RequireIfMatch(HttpRequest request)
    {
        var raw = request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw DomainException.Validation(
                ErrorCodes.PreconditionRequired,
                "This endpoint requires an If-Match header carrying the resource version.");
        }

        var trimmed = raw.Trim().Trim('W', '/').Trim('"');

        if (!long.TryParse(trimmed, out var version))
        {
            throw DomainException.Validation(
                ErrorCodes.PreconditionRequired, $"'{raw}' is not a valid version ETag.");
        }

        return version;
    }

    public static long? ReadIfMatch(HttpRequest request)
    {
        var raw = request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return RequireIfMatch(request);
    }
}
