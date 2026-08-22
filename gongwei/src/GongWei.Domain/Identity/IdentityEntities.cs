using System.Net;
using GongWei.Domain.Common;

namespace GongWei.Domain.Identity;

/// <summary>Table: users.</summary>
public class User : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>LINE subject. Never leaves the server, and never becomes a claim (§11).</summary>
    public string LineUserId { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public string Locale { get; set; } = "zh-TW";

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset? TermsAcceptedAt { get; set; }

    public DateTimeOffset? PrivacyAcceptedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Last activity, refreshed on a sliding window rather than on every request.</summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public ICollection<AdminRoleAssignment> AdminRoles { get; set; } = new List<AdminRoleAssignment>();

    public bool CanSignIn => Status == UserStatus.Active;
}

/// <summary>
/// Table: user_sessions. Only hashes are stored, so a database leak hands out no live
/// session and no CSRF secret (§11). Idle and absolute expiry are tracked separately.
/// </summary>
public class UserSession : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public byte[] CsrfSecretHash { get; set; } = null!;

    public IPAddress? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset IdleExpiresAt { get; set; }

    public DateTimeOffset AbsoluteExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokeReason { get; set; }

    public bool IsUsableAt(DateTimeOffset now) =>
        RevokedAt is null && IdleExpiresAt > now && AbsoluteExpiresAt > now;
}

/// <summary>
/// Table: admin_role_assignments. Roles may be time-limited via expires_at.
///
/// v1.1 added the public 執事 fields: an admin may opt in to being listed publicly with
/// a display name, title and duty. Nothing is published unless <see cref="IsPublic"/> is
/// set, and the CHECK refuses that without a display name.
/// </summary>
public class AdminRoleAssignment : IVersioned
{
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public AdminRole Role { get; set; }

    public Guid? GrantedBy { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string? PublicDisplayName { get; set; }

    public string? PublicTitle { get; set; }

    public string? PublicDuty { get; set; }

    public bool IsPublic { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsActiveAt(DateTimeOffset now) => ExpiresAt is null || ExpiresAt > now;

    /// <summary>Listed on the public 執事 page only when opted in and still in force.</summary>
    public bool IsPubliclyListedAt(DateTimeOffset now) =>
        IsPublic && PublicDisplayName is not null && IsActiveAt(now);
}

/// <summary>
/// Table: line_login_attempts — durable state for one LINE Login round trip, added in
/// v1.1 so state/nonce survive an app-pool recycle and can be replay-checked.
///
/// Only hashes and a Data-Protection-sealed payload are stored, and the return URL is
/// constrained by a CHECK to the player front end, which closes the open-redirect hole.
/// </summary>
public class LineLoginAttempt : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public byte[] StateHash { get; set; } = null!;

    public byte[] NonceHash { get; set; } = null!;

    /// <summary>Data-Protection-sealed blob holding the PKCE verifier and nonce.</summary>
    public byte[] ProtectedPayload { get; set; } = null!;

    public string ReturnUrl { get; set; } = null!;

    public System.Net.IPAddress? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set the moment the attempt is redeemed; a second redemption is a replay.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    public string? FailureCode { get; set; }

    public bool IsRedeemableAt(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
}

/// <summary>Table: preset_portraits — the official illustration set.</summary>
public class PresetPortrait : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public CharacterRole Role { get; set; }

    public string DisplayName { get; set; } = null!;

    public string AssetUrl { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>jsonb</summary>
    public string Metadata { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;
}

/// <summary>
/// Table: media_assets. Bytes live on the media volume or object storage; PostgreSQL
/// keeps only ownership, integrity, dimensions and moderation metadata (§2.1).
/// </summary>
public class MediaAsset : IVersioned, IHasId
{
    public const long MaxByteSize = 8 * 1024 * 1024;
    public const int MinWidth = 600;
    public const int MinHeight = 800;

    /// <summary>
    /// Not a schema constraint, but the processor downscales past this so one upload
    /// cannot pin a CPU core re-encoding a 20-megapixel image.
    /// </summary>
    public const int MaxDimension = 4096;

    public static readonly string[] AllowedUploadMimeTypes = ["image/jpeg", "image/png", "image/webp"];

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OwnerUserId { get; set; }

    public User? Owner { get; set; }

    /// <summary>Unguessable key. Never derived from the uploaded file name, never returned to a client.</summary>
    public string StorageKey { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string OriginalMimeType { get; set; } = null!;

    public string? StoredMimeType { get; set; }

    public long ByteSize { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>Lowercase hex, 64 chars — the schema stores char(64), not bytea.</summary>
    public string Sha256 { get; set; } = null!;

    public MediaAssetStatus Status { get; set; } = MediaAssetStatus.Uploaded;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsServable => Status == MediaAssetStatus.Ready;
}

/// <summary>Table: player_portrait_submissions. One submission per media asset.</summary>
public class PlayerPortraitSubmission : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public Guid MediaAssetId { get; set; }

    public MediaAsset? MediaAsset { get; set; }

    public CharacterRole Role { get; set; }

    // Normalised 0..1 crop rectangle relative to the original image.
    public decimal CropX { get; set; }

    public decimal CropY { get; set; }

    public decimal CropWidth { get; set; } = 1m;

    public decimal CropHeight { get; set; } = 1m;

    public PortraitSubmissionStatus Status { get; set; } = PortraitSubmissionStatus.Pending;

    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>The player may adjust the crop or withdraw only while pending (§6.8).</summary>
    public bool IsPlayerEditable => Status == PortraitSubmissionStatus.Pending;

    public bool IsUsableForCharacter => Status == PortraitSubmissionStatus.Approved;

    /// <summary>
    /// Mirrors the two CHECK constraints, which allow a 1.00001 epsilon so a legitimate
    /// full-frame crop is not rejected by float rounding in the client.
    /// </summary>
    public void EnsureCropIsValid()
    {
        const decimal epsilon = 1.00001m;

        var ok = CropX >= 0 && CropX <= 1
            && CropY >= 0 && CropY <= 1
            && CropWidth > 0 && CropWidth <= 1
            && CropHeight > 0 && CropHeight <= 1
            && CropX + CropWidth <= epsilon
            && CropY + CropHeight <= epsilon;

        if (!ok)
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["crop"] = ["裁切範圍必須落在原圖的 0–1 座標內。"]
            });
        }
    }
}
