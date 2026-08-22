using System.Data;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Identity;

public sealed record CropRect(decimal X, decimal Y, decimal Width, decimal Height);

/// <summary>
/// Player-uploaded character portraits (§6.8). The upload is decoded, stripped and
/// re-encoded before any row exists, so a stored media_assets row always describes a real
/// sanitised WebP on the media volume.
/// </summary>
public sealed class PortraitService(
    IGongWeiDb db,
    IClock clock,
    IRandomProvider random,
    ICurrentUser currentUser,
    IMediaStorage storage,
    IImageProcessor imageProcessor,
    IAuditWriter audit,
    IOutboxWriter outbox)
{
    public async Task<PlayerPortraitSubmission> UploadAsync(
        Stream upload,
        string originalFileName,
        string declaredContentType,
        CharacterRole role,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        // Decode, strip metadata, guard against decode bombs and re-encode. The processor
        // throws a DomainException for anything the player got wrong.
        var processed = await imageProcessor.ProcessPortraitAsync(upload, ct);

        // Unguessable key — never derived from the uploaded file name, never returned.
        var storageKey = $"portraits/{now:yyyy/MM}/{random.NextUrlSafeToken(24)}.webp";

        await using (var content = new MemoryStream(processed.Content, writable: false))
        {
            await storage.SaveAsync(storageKey, content, ct);
        }

        var asset = new MediaAsset
        {
            OwnerUserId = userId,
            StorageKey = storageKey,
            OriginalFileName = Truncate(originalFileName, 255),
            // Recorded as declared, but the pipeline trusted magic bytes, not this value.
            OriginalMimeType = NormaliseUploadMime(declaredContentType, processed.ContentType),
            StoredMimeType = processed.ContentType,
            ByteSize = processed.Content.LongLength,
            Width = processed.WidthPx,
            Height = processed.HeightPx,
            Sha256 = Convert.ToHexStringLower(processed.Sha256),
            Status = MediaAssetStatus.Ready,
            CreatedAt = now
        };

        db.MediaAssets.Add(asset);

        var submission = new PlayerPortraitSubmission
        {
            UserId = userId,
            MediaAssetId = asset.Id,
            Role = role,
            Status = PortraitSubmissionStatus.Pending,
            CreatedAt = now
        };

        submission.EnsureCropIsValid();
        db.PlayerPortraitSubmissions.Add(submission);

        audit.Write("portrait.upload", "player_portrait_submission", submission.Id, after: new
        {
            submission.MediaAssetId,
            asset.ByteSize,
            asset.Width,
            asset.Height,
            role = EnumNaming.ToDbValue(role)
        });

        outbox.Enqueue("portrait.submitted", "player_portrait_submission", submission.Id,
            new { submissionId = submission.Id, userId });

        await db.SaveChangesAsync(ct);
        return submission;
    }

    public async Task<PlayerPortraitSubmission> UpdateCropAsync(
        Guid submissionId,
        long expectedVersion,
        CropRect crop,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();

        var submission = await LoadOwnAsync(submissionId, userId, ct);
        submission.EnsureVersion(expectedVersion);

        if (!submission.IsPlayerEditable)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"此圖片已 {EnumNaming.ToDbValue(submission.Status)}，無法調整裁切。");
        }

        submission.CropX = crop.X;
        submission.CropY = crop.Y;
        submission.CropWidth = crop.Width;
        submission.CropHeight = crop.Height;
        submission.EnsureCropIsValid();

        await db.SaveChangesAsync(ct);
        return submission;
    }

    /// <summary>
    /// Withdraws a pending upload. The file is left on disk for the retention window and
    /// removed later by the cleanup job; a submission an application still references
    /// cannot be withdrawn (§6.8 step 7).
    /// </summary>
    public async Task<PlayerPortraitSubmission> WithdrawAsync(
        Guid submissionId,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();

        var submission = await LoadOwnAsync(submissionId, userId, ct);

        if (!submission.IsPlayerEditable)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"此圖片已 {EnumNaming.ToDbValue(submission.Status)}。");
        }

        var referenced = await db.CharacterApplications.AnyAsync(
            a => a.PlayerPortraitSubmissionId == submissionId
                 && a.Status != ApplicationStatus.Cancelled
                 && a.Status != ApplicationStatus.Rejected,
            ct);

        if (referenced)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, "此圖片已被建角申請引用，請先修改申請再撤回。");
        }

        submission.Status = PortraitSubmissionStatus.Withdrawn;

        audit.Write("portrait.withdraw", "player_portrait_submission", submission.Id);
        await db.SaveChangesAsync(ct);

        return submission;
    }

    /// <summary>
    /// Reviewer decision. Rejecting a portrait an application already points at sends that
    /// application back for revision in the same transaction (§6.8 step 5).
    /// </summary>
    public async Task<PlayerPortraitSubmission> ReviewAsync(
        Guid submissionId,
        bool approve,
        string? note,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.CharacterReviewer, AdminRole.Moderator);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["note"] = ["駁回必須說明原因"]
            });
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.LockRowAsync("player_portrait_submissions", submissionId, ct);

        var submission = await db.PlayerPortraitSubmissions
                             .FirstOrDefaultAsync(s => s.Id == submissionId, ct)
                         ?? throw DomainException.NotFound("Portrait upload", submissionId);

        if (submission.Status != PortraitSubmissionStatus.Pending)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"此圖片已 {EnumNaming.ToDbValue(submission.Status)}。");
        }

        submission.Status = approve
            ? PortraitSubmissionStatus.Approved
            : PortraitSubmissionStatus.Rejected;
        submission.ReviewedBy = reviewerId;
        submission.ReviewedAt = now;
        submission.ReviewNote = note;

        if (!approve)
        {
            // Anything still pointing at this portrait has to go back to the player.
            var referencing = await db.CharacterApplications
                .Where(a => a.PlayerPortraitSubmissionId == submissionId
                            && (a.Status == ApplicationStatus.Submitted
                                || a.Status == ApplicationStatus.Draft))
                .ToListAsync(ct);

            foreach (var application in referencing)
            {
                if (application.Status == ApplicationStatus.Submitted)
                {
                    application.Status = ApplicationStatus.NeedsRevision;
                    application.ReviewedBy = reviewerId;
                    application.ReviewedAt = now;
                }

                application.ReviewNote = $"人物圖片未通過審核：{note}";
            }
        }

        db.Notifications.Add(new Domain.Operations.Notification
        {
            UserId = submission.UserId,
            NotificationType = approve ? "portrait.approved" : "portrait.rejected",
            Title = approve ? "人物圖片已通過審核" : "人物圖片未通過審核",
            Body = note ?? string.Empty,
            Route = "/character/portrait",
            CreatedAt = now
        });

        audit.Write("portrait.review", "player_portrait_submission", submission.Id,
            after: new { status = EnumNaming.ToDbValue(submission.Status) },
            reason: note);

        outbox.Enqueue("portrait.reviewed", "player_portrait_submission", submission.Id, new
        {
            submissionId = submission.Id,
            submission.UserId,
            status = EnumNaming.ToDbValue(submission.Status)
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return submission;
    }

    /// <summary>
    /// Resolves the storage key a media endpoint may stream. The owner sees their own
    /// pending upload, reviewers see everything, and everyone else only sees a portrait
    /// that is approved and attached to a visible character (§6.8 step 7).
    /// </summary>
    public async Task<(string StorageKey, string ETag)> ResolveServableAsync(
        Guid mediaAssetId,
        CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.FirstOrDefaultAsync(a => a.Id == mediaAssetId, ct)
                    ?? throw DomainException.NotFound("Media asset", mediaAssetId);

        if (!asset.IsServable)
        {
            throw DomainException.NotFound("Media asset", mediaAssetId);
        }

        var isOwner = currentUser.UserId == asset.OwnerUserId;
        var isReviewer = currentUser.HasRole(AdminRole.CharacterReviewer)
                         || currentUser.HasRole(AdminRole.Moderator)
                         || currentUser.HasRole(AdminRole.SuperAdmin);

        if (!isOwner && !isReviewer)
        {
            var isPublished = await db.Characters.AnyAsync(
                c => c.PlayerPortraitSubmission!.MediaAssetId == mediaAssetId
                     && c.PlayerPortraitSubmission.Status == PortraitSubmissionStatus.Approved
                     && c.Status != CharacterStatus.Archived,
                ct);

            if (!isPublished)
            {
                throw DomainException.NotFound("Media asset", mediaAssetId);
            }
        }

        // The content hash doubles as a strong ETag — the bytes never change in place.
        return (asset.StorageKey, $"\"{asset.Sha256}\"");
    }

    private async Task<PlayerPortraitSubmission> LoadOwnAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var submission = await db.PlayerPortraitSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct)
                         ?? throw DomainException.NotFound("Portrait upload", id);

        if (submission.UserId != userId)
        {
            throw DomainException.NotFound("Portrait upload", id);
        }

        return submission;
    }

    /// <summary>
    /// The stored column only allows the three upload types, so an odd declared value
    /// falls back to what the processor actually produced.
    /// </summary>
    private static string NormaliseUploadMime(string declared, string processed) =>
        MediaAsset.AllowedUploadMimeTypes.Contains(declared) ? declared : processed;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
