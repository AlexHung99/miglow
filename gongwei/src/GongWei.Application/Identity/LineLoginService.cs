using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Identity;

/// <summary>Where to send the browser, and the state to redeem when it comes back.</summary>
public sealed record LineLoginStart(Uri AuthorizeUri);

/// <summary>
/// The result of a callback. Exactly one of <see cref="Session"/> and
/// <see cref="ErrorCode"/> is set, and <see cref="RedirectUrl"/> is always safe to send
/// as a Location header because it has been through <see cref="ReturnUrlPolicy"/>.
/// </summary>
public sealed record LineLoginCompletion(
    string RedirectUrl,
    IssuedSessionToken? Session,
    string? ErrorCode)
{
    public bool Succeeded => Session is not null;

    public static LineLoginCompletion Success(string redirectUrl, IssuedSessionToken session) =>
        new(redirectUrl, session, null);

    public static LineLoginCompletion Failure(string? returnUrl, string errorCode) =>
        new(ReturnUrlPolicy.ErrorRedirect(returnUrl, errorCode), null, errorCode);
}

/// <summary>
/// LINE Login, start to finish (line_login_v1.1.md §3 and §4).
///
/// Two rules shape the whole class. First, the attempt is consumed before the token is
/// exchanged, so a replayed state can never reach LINE. Second, nothing about a failure
/// travels back to the browser except a stable code — the detail goes to the log.
/// </summary>
public sealed class LineLoginService(
    IGongWeiDb db,
    ILineLoginClient line,
    ILineLoginAttemptStore attempts,
    IPayloadProtector protector,
    ISessionIssuer sessions,
    IAuditWriter audit,
    IClock clock,
    IRandomProvider random)
{
    /// <summary>Versioned so a future payload change cannot be unsealed by the old reader.</summary>
    public const string ProtectionPurpose = "GongWei.LineLogin.Attempt.v1";

    /// <summary>At most five live sessions per account; the oldest is revoked (§4.4 step 4).</summary>
    public const int MaxSessionsPerUser = 5;

    /// <summary>
    /// Validates the return URL, mints state/nonce/PKCE, and stores the attempt. The row
    /// is committed before the redirect goes out, so a callback that arrives on another
    /// worker process — or after an app-pool recycle — still finds it.
    /// </summary>
    public async Task<LineLoginStart> StartAsync(
        string? returnUrl,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var validated = ReturnUrlPolicy.Normalise(returnUrl);

        if (validated is null)
        {
            throw new DomainException(
                ErrorCodes.AuthReturnUrlInvalid,
                "回跳網址不在允許範圍內。",
                DomainErrorKind.Validation);
        }

        var state = random.NextUrlSafeToken(LoginAttemptPolicy.TokenByteCount);
        var nonce = random.NextUrlSafeToken(LoginAttemptPolicy.TokenByteCount);
        var verifier = random.NextUrlSafeToken(LoginAttemptPolicy.VerifierByteCount);

        var now = clock.UtcNow;

        var payload = JsonSerializer.Serialize(new AttemptPayload(nonce, verifier, validated, now));

        await attempts.CreateAsync(
            stateHash: Sha256(state),
            nonceHash: Sha256(nonce),
            protectedPayload: protector.Protect(ProtectionPurpose, payload),
            returnUrl: validated,
            ipAddress: ipAddress,
            userAgent: Truncate(userAgent, 512),
            expiresAt: now.Add(LoginAttemptPolicy.Lifetime),
            ct: ct);

        var authorizeUri = line.BuildAuthorizeUri(
            new LineAuthorizeRequest(state, nonce, CodeChallenge(verifier)));

        return new LineLoginStart(authorizeUri);
    }

    /// <summary>
    /// Redeems the state, exchanges the code, verifies the ID token, then creates the
    /// user and session in one transaction.
    ///
    /// Every failure path returns a <see cref="LineLoginCompletion"/> rather than throwing,
    /// because the browser is mid-redirect: a 500 here would strand the player on an
    /// error page belonging to the API rather than to the game.
    /// </summary>
    public async Task<LineLoginCompletion> CompleteAsync(
        string? state,
        string? code,
        string? lineError,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        if (string.IsNullOrEmpty(state))
        {
            return LineLoginCompletion.Failure(null, ErrorCodes.AuthStateInvalid);
        }

        // Consume first, whatever else happened: a state that reached us has been used,
        // even if LINE reported an error alongside it.
        var consumed = await attempts.ConsumeAsync(Sha256(state), now, ct);

        if (consumed.Attempt is null)
        {
            var code_ = consumed.Status switch
            {
                LoginAttemptStatus.Expired => ErrorCodes.AuthStateExpired,
                LoginAttemptStatus.AlreadyConsumed => ErrorCodes.AuthStateReplayed,
                _ => ErrorCodes.AuthStateInvalid
            };

            return LineLoginCompletion.Failure(null, code_);
        }

        var attempt = consumed.Attempt;

        if (!string.IsNullOrEmpty(lineError))
        {
            // The player pressed cancel on LINE's consent screen. Not an error worth
            // alerting on, and LINE's own description never reaches the browser.
            await attempts.RecordFailureAsync(attempt.Id, ErrorCodes.LineAccessDenied, ct);
            return LineLoginCompletion.Failure(attempt.ReturnUrl, ErrorCodes.LineAccessDenied);
        }

        if (string.IsNullOrEmpty(code))
        {
            await attempts.RecordFailureAsync(attempt.Id, ErrorCodes.AuthStateInvalid, ct);
            return LineLoginCompletion.Failure(attempt.ReturnUrl, ErrorCodes.AuthStateInvalid);
        }

        var unsealed_ = protector.Unprotect(ProtectionPurpose, attempt.ProtectedPayload);

        if (unsealed_ is null)
        {
            await attempts.RecordFailureAsync(attempt.Id, ErrorCodes.AuthAttemptUnprotectFailed, ct);
            return LineLoginCompletion.Failure(attempt.ReturnUrl, ErrorCodes.AuthStateInvalid);
        }

        var payload = JsonSerializer.Deserialize<AttemptPayload>(unsealed_);

        if (payload is null)
        {
            await attempts.RecordFailureAsync(attempt.Id, ErrorCodes.AuthAttemptUnprotectFailed, ct);
            return LineLoginCompletion.Failure(attempt.ReturnUrl, ErrorCodes.AuthStateInvalid);
        }

        LineTokenSet tokens;

        try
        {
            tokens = await line.ExchangeCodeAsync(code, payload.CodeVerifier, ct);
        }
        catch (Exception)
        {
            await attempts.RecordFailureAsync(attempt.Id, ErrorCodes.AuthLineTokenFailed, ct);
            return LineLoginCompletion.Failure(attempt.ReturnUrl, ErrorCodes.AuthLineTokenFailed);
        }

        VerifiedLineIdentity identity;

        try
        {
            identity = await line.VerifyIdTokenAsync(tokens.IdToken, payload.Nonce, ct);
        }
        catch (Exception)
        {
            await attempts.RecordFailureAsync(attempt.Id, ErrorCodes.AuthIdTokenInvalid, ct);
            return LineLoginCompletion.Failure(attempt.ReturnUrl, ErrorCodes.AuthIdTokenInvalid);
        }

        // The return URL sealed into the payload is the one that was validated at start
        // time; the column is only a convenience for operators reading the table.
        var returnUrl = ReturnUrlPolicy.Normalise(payload.ReturnUrl) ?? ReturnUrlPolicy.Default;

        return await CreateSessionAsync(attempt.Id, identity, returnUrl, ipAddress, userAgent, now, ct);
    }

    /// <summary>
    /// Upserts the user, issues the session, trims old sessions and writes the audit row
    /// in a single transaction. A half-finished login must leave no user without a session
    /// and no session without an audit trail (§4.4).
    /// </summary>
    private async Task<LineLoginCompletion> CreateSessionAsync(
        Guid attemptId,
        VerifiedLineIdentity identity,
        string returnUrl,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == identity.Sub, ct);

        if (user is null)
        {
            user = new User
            {
                LineUserId = identity.Sub,
                DisplayName = Truncate(identity.DisplayName, 80) ?? "無名",
                AvatarUrl = identity.PictureUrl,
                Status = UserStatus.Active,
                CreatedAt = now,
                LastLoginAt = now,
                LastSeenAt = now
            };

            db.Users.Add(user);
        }
        else
        {
            var rejection = UserStatusPolicy.RejectionCode(user.Status);

            if (rejection is not null)
            {
                await attempts.RecordFailureAsync(attemptId, rejection, ct);
                await transaction.RollbackAsync(ct);
                return LineLoginCompletion.Failure(returnUrl, rejection);
            }

            // The LINE display name is the account label, never the in-game character
            // name — those live on characters and are not touched here (§4.4 step 1).
            user.DisplayName = Truncate(identity.DisplayName, 80) ?? user.DisplayName;
            user.AvatarUrl = identity.PictureUrl;
            user.LastLoginAt = now;
            user.LastSeenAt = now;
        }

        await db.SaveChangesAsync(ct);

        await RevokeSurplusSessionsAsync(user.Id, now, ct);

        var session = await sessions.IssueAsync(user.Id, isAdminSession: false, ipAddress, userAgent, ct);

        audit.Write(
            action: "auth.login",
            targetType: "user",
            targetId: user.Id,
            after: new { result = "success", sessionId = session.SessionId });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return LineLoginCompletion.Success(returnUrl, session);
    }

    /// <summary>
    /// Keeps at most <see cref="MaxSessionsPerUser"/> live sessions, revoking the oldest
    /// first. Counting before the new session is issued is why the limit is N-1 here.
    /// </summary>
    private async Task RevokeSurplusSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var live = await db.UserSessions
            .Where(s => s.UserId == userId
                        && s.RevokedAt == null
                        && s.IdleExpiresAt > now
                        && s.AbsoluteExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(MaxSessionsPerUser - 1)
            .ToListAsync(ct);

        foreach (var session in live)
        {
            session.RevokedAt = now;
            session.RevokeReason = "session_limit";
        }
    }

    /// <summary>PKCE S256: BASE64URL(SHA256(ASCII(verifier))), unpadded.</summary>
    private static string CodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    /// <summary>
    /// Sealed into <c>protected_payload</c>. Never logged, never returned, and never
    /// stored unencrypted — the database only holds hashes of the nonce and state.
    /// </summary>
    private sealed record AttemptPayload(
        string Nonce,
        string CodeVerifier,
        string ReturnUrl,
        DateTimeOffset CreatedAt);
}
