using GongWei.Api.Contracts;
using GongWei.Api.Http;
using GongWei.Application.Abstractions;
using GongWei.Application.Identity;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Infrastructure.Persistence;
using GongWei.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Controllers;

/// <summary>
/// LINE Login and session management (api_v1_v1.1 §2, line_login_v1.1).
///
/// Start and callback are browser redirects, not fetch calls: the SPA sets
/// <c>location.href</c> and the browser follows LINE's redirect back here. That is why
/// neither returns JSON on the happy path, and why failures redirect to the front end
/// with a stable code rather than rendering an API error page.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    LineLoginService login,
    SessionService sessions,
    GongWeiDbContext db,
    IClock clock,
    ILogger<AuthController> logger,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Begins LINE Login. Responds 302 to LINE's authorize endpoint once the attempt row
    /// is committed, so a callback handled by a different worker process still finds it.
    /// </summary>
    [HttpGet("line/start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Start([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var start = await login.StartAsync(returnUrl, ClientIp(), UserAgent(), ct);

        logger.LogInformation("LINE login started. RequestId={RequestId}", HttpContext.TraceIdentifier);

        // AbsoluteUri, never ToString(): ToString un-escapes non-reserved characters, which
        // turns scope=openid%20profile into a raw space in the Location header.
        //
        // 302, not 303: the browser is following a GET and must keep following a GET.
        return RedirectTo(start.AuthorizeUri.AbsoluteUri, StatusCodes.Status302Found);
    }

    /// <summary>
    /// LINE's redirect target. Registered in the LINE Console as
    /// <c>https://gongwei-api.miglow.vip/api/v1/auth/line/callback</c> — the value here,
    /// in the authorize request and in the token exchange must be identical.
    /// </summary>
    [HttpGet("line/callback")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        var completion = await login.CompleteAsync(state, code, error, ClientIp(), UserAgent(), ct);

        if (!completion.Succeeded)
        {
            // Only the stable code is logged, never the code, state or LINE's description.
            logger.LogWarning(
                "LINE login failed with {ErrorCode}. RequestId={RequestId}",
                completion.ErrorCode,
                HttpContext.TraceIdentifier);

            // 303 like the success path, so the front end sees one consistent shape.
            return SeeOther(completion.RedirectUrl);
        }

        IssueSessionCookie(completion.Session!);

        logger.LogInformation("LINE login succeeded. RequestId={RequestId}", HttpContext.TraceIdentifier);

        // 303 turns the redirect into a plain GET of the front end, per §4.4 step 6.
        return SeeOther(completion.RedirectUrl);
    }

    /// <summary>
    /// Current account, admin roles and character. This is the endpoint the SPA polls
    /// after the redirect: a null <c>currentCharacter</c> means "send them to register".
    /// </summary>
    [HttpGet("session")]
    [Authorize]
    public Task<ActionResult<MeResponse>> Session(CancellationToken ct) => MeAsync(ct);

    /// <summary>
    /// Hands the SPA a session-bound CSRF token. Each call rotates the secret, so the
    /// client should fetch once at start-up and again after a 403 <c>CSRF_INVALID</c>.
    /// </summary>
    [HttpGet("csrf")]
    [Authorize]
    public async Task<ActionResult<CsrfTokenResponse>> Csrf(CancellationToken ct)
    {
        var issued = await sessions.RotateCsrfAsync(RequireSessionId(), ct);

        // Returned in the body, never in a cookie: the SPA holds it in memory, so it
        // cannot be replayed by anything that merely reaches the browser's cookie jar.
        // The header name is published once through GET /meta, not repeated here (§14.1A).
        return Ok(new CsrfTokenResponse(issued.Token, issued.ExpiresAt));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sessions.RevokeAsync(RequireSessionId(), "logout", ct);

        ClearSessionCookie();
        return NoContent();
    }

    /// <summary>Revokes every session for the account — used after a suspected compromise.</summary>
    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var userId = RequireUserId();
        var revoked = await sessions.RevokeAllAsync(userId, "logout_all", ct);

        logger.LogInformation("Revoked {Count} sessions on logout-all.", revoked);

        ClearSessionCookie();
        return NoContent();
    }

    /// <summary>
    /// <c>GET /me</c> — the path api_v1_v1.1 §2 uses for the account summary. Absolute
    /// route, so it escapes this controller's <c>api/v1/auth</c> prefix.
    /// </summary>
    [HttpGet("/api/v1/me")]
    [Authorize]
    public Task<ActionResult<MeResponse>> Me(CancellationToken ct) => MeAsync(ct);

    private async Task<ActionResult<MeResponse>> MeAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        var now = clock.UtcNow;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw DomainException.NotFound("user", userId);

        var roles = await db.AdminRoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && (a.ExpiresAt == null || a.ExpiresAt > now))
            .Select(a => a.Role)
            .ToListAsync(ct);

        var character = await LoadCurrentCharacterAsync(userId, ct);

        var unread = await db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

        return Ok(new MeResponse(
            User: new MeUserResponse(
                user.Id,
                user.DisplayName,
                EnumNaming.ToDbValue(user.Status),
                // users has no preferences column in v1.1; the field stays in the contract
                // as an empty object so the front end does not have to special-case it.
                new Dictionary<string, object>(),
                user.Version),
            CharacterState: CharacterStateValues.From(character?.Status),
            Character: character is null
                ? null
                : MeCharacterResponse.From(character, PortraitUrl(character)),
            AdminRoles: roles.Select(EnumNaming.ToDbValue).ToList(),
            UnreadNotificationCount: unread,
            // Populated once the application/pause flows land (task #14); an empty list
            // is the correct answer today rather than a missing field.
            PendingActions: []));
    }

    private void IssueSessionCookie(IssuedSessionToken issued)
    {
        Response.Cookies.Append(
            configuration["Session:CookieName"] ?? "gw_session",
            issued.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                // Host-only on purpose: no Domain, so the cookie never reaches a
                // sibling subdomain of miglow.vip (line_login_v1.1 §5.1).
                Expires = issued.AbsoluteExpiresAt,
                Path = "/"
            });
    }

    private void ClearSessionCookie() =>
        Response.Cookies.Delete(
            configuration["Session:CookieName"] ?? "gw_session",
            new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });

    /// <summary>
    /// Cloudflare terminates TLS, so <c>RemoteIpAddress</c> is an edge address.
    /// <c>CF-Connecting-IP</c> is the only header that carries the real client, and it is
    /// only trustworthy because the origin is not reachable except through Cloudflare.
    /// </summary>
    private string? ClientIp() =>
        Request.Headers["CF-Connecting-IP"].FirstOrDefault()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private Guid RequireUserId() =>
        Guid.TryParse(User.FindFirst(GongWeiClaims.UserId)?.Value, out var id)
            ? id
            : throw new DomainException(
                ErrorCodes.AuthRequired, "請先登入。", DomainErrorKind.Unauthenticated);

    private Guid RequireSessionId() =>
        Guid.TryParse(User.FindFirst(GongWeiClaims.SessionId)?.Value, out var id)
            ? id
            : throw new DomainException(
                ErrorCodes.AuthRequired, "請先登入。", DomainErrorKind.Unauthenticated);

    private IActionResult SeeOther(string location) =>
        RedirectTo(location, StatusCodes.Status303SeeOther);

    /// <summary>
    /// Sets Location by hand instead of returning <see cref="RedirectResult"/>.
    ///
    /// MVC's redirect executor logs "redirecting to {url}" at Information, and on this
    /// controller that url is the LINE authorize request — complete with state and nonce,
    /// both of which line_login_v1.1 §6 forbids logging. Writing the header directly keeps
    /// the executor, and its log line, out of the path.
    /// </summary>
    private IActionResult RedirectTo(string location, int statusCode)
    {
        Response.Headers.Location = location;
        return StatusCode(statusCode);
    }

    /// <summary>
    /// The player's current character, including a dead one.
    ///
    /// Dead is deliberately not filtered out: §14.1 lists <c>dead</c> as a valid
    /// characterState, and the front end needs it to show the memorial and the
    /// re-application prompt rather than treating the player as brand new.
    /// </summary>
    private Task<Domain.Characters.Character?> LoadCurrentCharacterAsync(
        Guid userId,
        CancellationToken ct) =>
        db.Characters
            .AsNoTracking()
            .Include(c => c.Rank)
            .Include(c => c.Portrait)
            .Where(c => c.UserId == userId)
            // Newest first, so a player on their second character sees that one.
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// An official portrait is a public asset URL; a player-uploaded one is served
    /// through the API so visibility and moderation state are still enforced.
    /// </summary>
    internal static string? PortraitUrl(Domain.Characters.Character c) =>
        c.Portrait?.AssetUrl
        ?? (c.PlayerPortraitSubmissionId is { } id ? $"/api/v1/media/portraits/{id}" : null);
}
