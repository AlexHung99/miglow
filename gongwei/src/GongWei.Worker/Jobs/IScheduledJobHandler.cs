using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GongWei.Worker.Jobs;

/// <summary>
/// One scheduled job. Every implementation must be safe to re-run: a lease can expire
/// mid-flight and another worker will pick the job up (spec §10).
/// </summary>
public interface IScheduledJobHandler
{
    string JobKey { get; }

    /// <summary>Returns how many items were processed, for the job_runs record.</summary>
    Task<int> RunAsync(CancellationToken ct);
}

/// <summary>Retires sessions past their retention window.</summary>
public sealed class SessionCleanupJob(GongWeiDbContext db, IClock clock) : IScheduledJobHandler
{
    public string JobKey => "session-cleanup";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddDays(-30);

        return await db.UserSessions
            .Where(s => s.AbsoluteExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>
/// Sweeps spent LINE login attempts (line_login_v1.1 §8). Rows are kept for 24 hours past
/// expiry so a replay investigation still has something to look at; the audit log, which
/// is permanent, is what survives afterwards.
/// </summary>
public sealed class LoginAttemptCleanupJob(
    ILineLoginAttemptStore attempts,
    IClock clock) : IScheduledJobHandler
{
    public string JobKey => "login-attempt-cleanup";

    public Task<int> RunAsync(CancellationToken ct) =>
        attempts.PurgeExpiredAsync(
            clock.UtcNow - Domain.Identity.LoginAttemptPolicy.RetentionAfterExpiry, ct);
}

/// <summary>Removes only expired idempotency records — never live ones (spec §10).</summary>
public sealed class IdempotencyCleanupJob(GongWeiDbContext db, IClock clock) : IScheduledJobHandler
{
    public string JobKey => "idempotency-cleanup";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await db.IdempotencyRecords
            .Where(r => r.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>Moves event rooms through scheduled → open → locked as their windows pass.</summary>
public sealed class EventStateTransitionJob(GongWeiDbContext db, IClock clock) : IScheduledJobHandler
{
    public string JobKey => "event-state-transition";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        var opened = await db.EventRooms
            .Where(e => e.Status == EventRoomStatus.Scheduled && e.OpensAt <= now && e.DeadlineAt > now)
            .ExecuteUpdateAsync(
                e => e.SetProperty(x => x.Status, EventRoomStatus.Open)
                      .SetProperty(x => x.UpdatedAt, now)
                      .SetProperty(x => x.Version, x => x.Version + 1),
                ct);

        var locked = await db.EventRooms
            .Where(e => e.Status == EventRoomStatus.Open && e.DeadlineAt <= now)
            .ExecuteUpdateAsync(
                e => e.SetProperty(x => x.Status, EventRoomStatus.Locked)
                      .SetProperty(x => x.UpdatedAt, now)
                      .SetProperty(x => x.Version, x => x.Version + 1),
                ct);

        return opened + locked;
    }
}

/// <summary>
/// Notifies players that a pregnancy is due. The draw itself stays a deliberate admin
/// action — nothing automated decides who is born (spec §1.3).
/// </summary>
public sealed class PregnancyDueJob(
    GongWeiDbContext db,
    IClock clock,
    IOutboxWriter outbox,
    ILogger<PregnancyDueJob> logger) : IScheduledJobHandler
{
    public string JobKey => "pregnancy-due";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        var due = await db.Pregnancies
            .Where(p => p.Status == PregnancyStatus.Ongoing && p.DueAt <= now)
            .Select(p => new { p.Id, p.MotherCharacterId, p.DueAt })
            .ToListAsync(ct);

        foreach (var pregnancy in due)
        {
            outbox.Enqueue("pregnancy.due", "pregnancy", pregnancy.Id, new
            {
                pregnancyId = pregnancy.Id,
                motherCharacterId = pregnancy.MotherCharacterId,
                dueAt = pregnancy.DueAt
            });
        }

        if (due.Count > 0)
        {
            logger.LogInformation("{Count} pregnancies are due and awaiting a GM draw", due.Count);
            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }
}

/// <summary>Expires status effects whose window has passed.</summary>
public sealed class StatusEffectResolveJob(GongWeiDbContext db, IClock clock) : IScheduledJobHandler
{
    public string JobKey => "status-effect-resolve";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        // v1.1 has no status column on status_effects: an effect is live while
        // resolved_at is null, so resolving one is simply stamping that column.
        return await db.StatusEffects
            .Where(e => e.ResolvedAt == null
                        && e.ExpiresAt != null
                        && e.ExpiresAt <= now)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.ResolvedAt, now), ct);
    }
}

/// <summary>
/// Deletes portrait files that have been withdrawn or rejected and are past their
/// retention window, and which nothing references any more (spec §6.8 step 7).
/// </summary>
public sealed class MediaPurgeJob(
    GongWeiDbContext db,
    IClock clock,
    IMediaStorage storage,
    ILogger<MediaPurgeJob> logger) : IScheduledJobHandler
{
    /// <summary>
    /// How long a quarantined asset stays on disk. Long enough for a moderator to review
    /// a contested decision, short enough not to hoard rejected uploads.
    /// </summary>
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public string JobKey => "media-purge";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cutoff = now - Retention;

        // v1.1 tracks lifecycle through status alone — there is no purge_after or
        // deleted_at column, so "quarantined and untouched since the cutoff" is the
        // criterion, and the status flips to deleted once the bytes are gone.
        var purgeable = await db.MediaAssets
            .Where(a => a.Status == MediaAssetStatus.Quarantined && a.UpdatedAt <= cutoff)
            .Where(a => !db.PlayerPortraitSubmissions.Any(s => s.MediaAssetId == a.Id))
            .Take(200)
            .ToListAsync(ct);

        var deleted = 0;

        foreach (var asset in purgeable)
        {
            try
            {
                await storage.DeleteAsync(asset.StorageKey, ct);
                asset.Status = MediaAssetStatus.Deleted;
                deleted++;
            }
            catch (IOException ex)
            {
                // Leave the row alone so the next run retries rather than losing track
                // of a file that is still on disk.
                logger.LogWarning(ex, "Could not delete media {AssetId}", asset.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        return deleted;
    }
}
