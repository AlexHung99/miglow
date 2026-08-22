using GongWei.Domain.Common;
using GongWei.Domain.Operations;

namespace GongWei.Application.Abstractions;

/// <summary>UTC clock. Injected so tests can move time without sleeping.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Cryptographically secure randomness. Used for session tokens, storage keys and —
/// critically — the birth draw, which must be auditable and unbiased (spec §6.4).
/// </summary>
public interface IRandomProvider
{
    /// <summary>Uniform in [0, exclusiveUpperBound).</summary>
    int NextInt(int exclusiveUpperBound);

    byte[] NextBytes(int count);

    string NextUrlSafeToken(int byteCount = 32);
}

/// <summary>Who is making the current request, as established by the session cookie.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    IReadOnlySet<AdminRole> AdminRoles { get; }

    string? RequestId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool HasRole(AdminRole role);

    Guid RequireUserId();

    void RequireRole(params AdminRole[] anyOf);
}

/// <summary>
/// Where a processed portrait actually lives. Never the IIS web root and never a
/// Postgres bytea column (spec §2.1).
/// </summary>
public interface IMediaStorage
{
    Task<string> SaveAsync(string storageKey, Stream content, CancellationToken ct = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>Result of decoding, sanitising and re-encoding an uploaded image (spec §6.8).</summary>
public sealed record ProcessedImage(
    byte[] Content,
    string ContentType,
    int WidthPx,
    int HeightPx,
    byte[] Sha256);

/// <summary>
/// Validates by magic bytes (never by extension or Content-Type), strips metadata,
/// rejects decode bombs and re-encodes to WebP.
/// </summary>
public interface IImageProcessor
{
    Task<ProcessedImage> ProcessPortraitAsync(Stream upload, CancellationToken ct = default);
}

public sealed record LineAuthorizeRequest(string State, string Nonce, string CodeChallenge);

/// <summary>
/// What survives a token exchange. The access and refresh tokens are deliberately absent:
/// line_login_v1.1 §4.4 requires them to be discarded the moment verification finishes, so
/// this type gives no caller the option of persisting one.
/// </summary>
public sealed record LineTokenSet(string IdToken);

/// <summary>An identity LINE has verified. <paramref name="Sub"/> is the only stable key.</summary>
public sealed record VerifiedLineIdentity(string Sub, string DisplayName, string? PictureUrl);

public interface ILineLoginClient
{
    Uri BuildAuthorizeUri(LineAuthorizeRequest request);

    Task<LineTokenSet> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default);

    /// <summary>
    /// Verifies the ID token through LINE's own verify endpoint, checking issuer,
    /// audience, expiry and — critically — that the nonce matches the one this server
    /// generated. Decoding the payload without verifying would accept any forged token.
    /// </summary>
    Task<VerifiedLineIdentity> VerifyIdTokenAsync(string idToken, string nonce, CancellationToken ct = default);
}

/// <summary>Why a state value could not be redeemed. Each maps to a distinct error code.</summary>
public enum LoginAttemptStatus
{
    Consumed,
    NotFound,
    Expired,
    AlreadyConsumed
}

/// <summary>The parts of a redeemed attempt the callback still needs.</summary>
public sealed record RedeemedLoginAttempt(Guid Id, byte[] ProtectedPayload, string ReturnUrl);

public sealed record LoginAttemptConsumeResult(LoginAttemptStatus Status, RedeemedLoginAttempt? Attempt);

public interface ILineLoginAttemptStore
{
    Task CreateAsync(
        byte[] stateHash,
        byte[] nonceHash,
        byte[] protectedPayload,
        string returnUrl,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically marks the attempt consumed and returns it. Must be one conditional
    /// UPDATE, never a read followed by a write: two callbacks arriving together on the
    /// same state would both pass a read-then-write check (line_login_v1.1 §7).
    /// </summary>
    Task<LoginAttemptConsumeResult> ConsumeAsync(
        byte[] stateHash,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>Records why a redeemed attempt went on to fail. Never un-consumes it.</summary>
    Task RecordFailureAsync(Guid attemptId, string failureCode, CancellationToken ct = default);

    Task<int> PurgeExpiredAsync(DateTimeOffset expiredBefore, CancellationToken ct = default);
}

/// <summary>
/// Seals the nonce and PKCE verifier so they survive an app-pool recycle without the
/// database ever holding them in the clear (line_login_v1.1 §3.2).
/// </summary>
public interface IPayloadProtector
{
    byte[] Protect(string purpose, string plaintext);

    /// <summary>Returns null when the payload cannot be unsealed — a rotated key or tampering.</summary>
    string? Unprotect(string purpose, byte[] sealedPayload);
}

public enum PasswordVerification
{
    Failed,
    Succeeded,

    /// <summary>Correct, but hashed with weaker parameters than the current default.</summary>
    SucceededNeedsRehash
}

/// <summary>
/// Password hashing for the local admin credential. Deliberately narrow: this is the only
/// password in the system and nothing else should acquire one.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string password, string encoded);
}

/// <summary>A session that has just been created. The raw token exists only here.</summary>
public sealed record IssuedSessionToken(
    Guid SessionId,
    string Token,
    string CsrfSecret,
    DateTimeOffset AbsoluteExpiresAt);

/// <summary>
/// Issues player and admin sessions. Abstracted so the login flow can stay in the
/// Application layer while the hashing and storage details stay in Infrastructure.
/// </summary>
public interface ISessionIssuer
{
    Task<IssuedSessionToken> IssueAsync(
        Guid userId,
        bool isAdminSession,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);
}

/// <summary>
/// Writes an audit row inside the business transaction, never after it. The tracked
/// entity is returned so a caller can read its identity after SaveChanges — several
/// endpoints must return <c>auditLogId</c> alongside the business result (§6.11 step 5).
/// </summary>
public interface IAuditWriter
{
    AuditLog Write(
        string action,
        string? targetType = null,
        Guid? targetId = null,
        object? before = null,
        object? after = null,
        string? reason = null);
}

/// <summary>
/// Queues a post-commit message in the same transaction as the change (§10). The
/// aggregate type and id are stored alongside the topic so a stuck message can be traced
/// back to the row that produced it.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue(string topic, string aggregateType, Guid aggregateId, object payload);
}

/// <summary>Serialises the jsonb columns and audit snapshots consistently.</summary>
public interface IJsonSerializer
{
    string Serialize(object? value);

    T? Deserialize<T>(string json);

    bool IsValidObject(string json);

    bool IsValidArray(string json);
}
