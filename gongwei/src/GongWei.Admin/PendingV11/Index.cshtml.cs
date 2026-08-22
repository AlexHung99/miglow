using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages;

public sealed class IndexModel(GongWeiDbContext db) : PageModel
{
    public int PendingApplications { get; private set; }

    public int PendingPortraits { get; private set; }

    public int PendingAudiences { get; private set; }

    public int PendingApprovals { get; private set; }

    public int DuePregnancies { get; private set; }

    public int OpenEvents { get; private set; }

    public int ActiveCharacters { get; private set; }

    public int WaitingHeirs { get; private set; }

    /// <summary>Dead outbox messages need a human; they are never retried automatically.</summary>
    public int DeadOutboxMessages { get; private set; }

    public int FailingJobs { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        PendingApplications = await db.CharacterApplications
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, ct);
        PendingPortraits = await db.PlayerPortraitSubmissions
            .CountAsync(s => s.ReviewStatus == PortraitReviewStatus.Pending, ct);
        PendingAudiences = await db.AudienceRequests
            .CountAsync(r => r.Status == AudienceStatus.Pending, ct);
        PendingApprovals = await db.ApprovalRequests
            .CountAsync(r => r.Status == ApprovalStatus.Pending || r.Status == ApprovalStatus.Approved, ct);
        DuePregnancies = await db.Pregnancies
            .CountAsync(p => p.Status == PregnancyStatus.Ongoing && p.DueAt <= now, ct);
        OpenEvents = await db.EventRooms
            .CountAsync(e => e.Status == EventRoomStatus.Open, ct);
        ActiveCharacters = await db.Characters
            .CountAsync(c => c.Status == CharacterStatus.Active, ct);
        WaitingHeirs = await db.HeirWaitPoolEntries
            .CountAsync(e => e.Status == WaitPoolStatus.Waiting, ct);
        DeadOutboxMessages = await db.OutboxMessages
            .CountAsync(m => m.Status == OutboxStatus.Dead, ct);
        FailingJobs = await db.ScheduledJobs
            .CountAsync(j => j.ConsecutiveFailures >= 3, ct);
    }
}
