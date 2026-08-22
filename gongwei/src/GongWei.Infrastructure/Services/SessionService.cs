using System.Net;
using System.Security.Cryptography;
using System.Text;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Infrastructure.Services;

/// <summary>A freshly minted CSRF secret. The plaintext exists only in this record.</summary>
public sealed record IssuedCsrfToken(string Token, DateTimeOffset ExpiresAt);

public sealed record AuthenticatedPrincipal(
    Guid UserId,
    Guid SessionId,
    string DisplayName,
    IReadOnlySet<AdminRole> AdminRoles);

/// <summary>
/// Session issue, lookup and revocation. Only SHA-256 hashes are stored, so a database
/// leak yields neither a live session nor a usable CSRF secret (§11).
/// </summary>
public sealed class SessionService(GongWeiDbContext db, IClock clock, IRandomProvider random)
    : ISessionIssuer
{
    /// <summary>Player sessions: 7 days of inactivity, 30 days absolute (line_login_v1.1 §5.1).</summary>
    public static readonly TimeSpan PlayerIdleTimeout = TimeSpan.FromDays(7);
    public static readonly TimeSpan PlayerAbsoluteLifetime = TimeSpan.FromDays(30);

    /// <summary>Admin sessions time out far sooner (§11).</summary>
    public static readonly TimeSpan AdminIdleTimeout = TimeSpan.FromHours(2);
    public static readonly TimeSpan AdminAbsoluteLifetime = TimeSpan.FromHours(8);

    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    public async Task<IssuedSessionToken> IssueAsync(
        Guid userId,
        bool isAdminSession,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var token = random.NextUrlSafeToken(32);
        var csrfSecret = random.NextUrlSafeToken(32);

        var idle = isAdminSession ? AdminIdleTimeout : PlayerIdleTimeout;
        var absolute = isAdminSession ? AdminAbsoluteLifetime : PlayerAbsoluteLifetime;

        var session = new UserSession
        {
            UserId = userId,
            TokenHash = Hash(token),
            CsrfSecretHash = Hash(csrfSecret),
            IpAddress = ParseIp(ipAddress),
            UserAgent = Truncate(userAgent, 512),
            CreatedAt = now,
            LastSeenAt = now,
            IdleExpiresAt = now.Add(idle),
            AbsoluteExpiresAt = now.Add(absolute)
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return new IssuedSessionToken(session.Id, token, csrfSecret, session.AbsoluteExpiresAt);
    }

    /// <summary>
    /// Resolves a raw cookie token, sliding the idle window. Returns null for anything
    /// unusable rather than throwing, so an expired cookie reads as "not signed in".
    /// </summary>
    public async Task<AuthenticatedPrincipal?> ResolveAsync(
        string token,
        bool isAdminSession,
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var hash = Hash(token);

        var session = await db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        if (session?.User is null || !session.IsUsableAt(now) || !session.User.CanSignIn)
        {
            return null;
        }

        var roles = await ActiveRolesAsync(session.UserId, now, ct);

        // An admin cookie is only good for an account that still holds an admin role.
        if (isAdminSession && roles.Count == 0)
        {
            return null;
        }

        // Slide the idle window, but write at most once every few minutes so a read-heavy
        // page does not turn every request into an UPDATE.
        if (now - session.LastSeenAt > TimeSpan.FromMinutes(5))
        {
            var idle = isAdminSession ? AdminIdleTimeout : PlayerIdleTimeout;

            session.LastSeenAt = now;
            session.IdleExpiresAt = Min(now.Add(idle), session.AbsoluteExpiresAt);
            await db.SaveChangesAsync(ct);
        }

        return new AuthenticatedPrincipal(
            session.UserId, session.Id, session.User.DisplayName, roles);
    }

    /// <summary>Constant-time comparison of the CSRF token against the stored secret hash.</summary>
    public async Task<bool> ValidateCsrfAsync(
        Guid sessionId,
        string presentedSecret,
        CancellationToken ct = default)
    {
        var storedHash = await db.UserSessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.CsrfSecretHash)
            .FirstOrDefaultAsync(ct);

        return storedHash is not null
            && CryptographicOperations.FixedTimeEquals(storedHash, Hash(presentedSecret));
    }

    /// <summary>
    /// Mints a fresh CSRF secret for a session and returns the plaintext once.
    ///
    /// Rotation is necessary because only the hash is stored, so the original secret
    /// cannot be handed out again after issue. A second browser tab calling this will
    /// invalidate the first tab's token; the SPA handles that by re-fetching on a
    /// 403 CSRF_INVALID and retrying, which is the documented client behaviour.
    /// </summary>
    public async Task<IssuedCsrfToken> RotateCsrfAsync(Guid sessionId, CancellationToken ct = default)
    {
        var secret = random.NextUrlSafeToken(32);

        var session = await db.UserSessions
            .Where(s => s.Id == sessionId && s.RevokedAt == null)
            .Select(s => new { s.IdleExpiresAt, s.AbsoluteExpiresAt })
            .FirstOrDefaultAsync(ct);

        if (session is null)
        {
            throw new DomainException(
                ErrorCodes.SessionExpired,
                "登入階段已失效，請重新登入。",
                DomainErrorKind.Unauthenticated);
        }

        var updated = await db.UserSessions
            .Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CsrfSecretHash, Hash(secret)), ct);

        if (updated == 0)
        {
            throw new DomainException(
                ErrorCodes.SessionExpired,
                "登入階段已失效，請重新登入。",
                DomainErrorKind.Unauthenticated);
        }

        // The reported expiry is the session's, because that is what actually governs the
        // token: there is no separate CSRF expiry column, so claiming a shorter lifetime
        // would be telling the client something this server does not enforce. The token
        // also dies early on the next rotation, on logout, and on revocation.
        var expiresAt = Min(session.IdleExpiresAt, session.AbsoluteExpiresAt);

        return new IssuedCsrfToken(secret, expiresAt);
    }

    public async Task RevokeAsync(Guid sessionId, string reason, CancellationToken ct = default)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = clock.UtcNow;
        session.RevokeReason = Truncate(reason, 200);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Kills every live session for a user. Called on logout-all, on suspension, and when
    /// an admin role is removed (§11).
    /// </summary>
    public async Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var truncated = Truncate(reason, 200);

        return await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokeReason, truncated),
                ct);
    }

    public async Task<HashSet<AdminRole>> ActiveRolesAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var roles = await db.AdminRoleAssignments
            .Where(a => a.UserId == userId && (a.ExpiresAt == null || a.ExpiresAt > now))
            .Select(a => a.Role)
            .ToListAsync(ct);

        return roles.ToHashSet();
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static IPAddress? ParseIp(string? value) =>
        IPAddress.TryParse(value, out var address) ? address : null;

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
