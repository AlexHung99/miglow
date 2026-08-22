using System.Net;
using GongWei.Domain.Common;

namespace GongWei.Domain.Operations;

/// <summary>Table: notifications.</summary>
public class Notification : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    /// <summary>Content-driven type string, e.g. application.approved.</summary>
    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    /// <summary>Relative in-app route only — never an absolute URL from user content (§11).</summary>
    public string? Route { get; set; }

    public string Payload { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsUnread => ReadAt is null;
}

/// <summary>Table: announcements.</summary>
public class Announcement : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Title { get; set; } = null!;

    public string BodyMarkdown { get; set; } = null!;

    public AnnouncementSeverity Severity { get; set; } = AnnouncementSeverity.Info;

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.All;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public Guid PublishedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsVisibleAt(DateTimeOffset now) =>
        StartsAt <= now && (EndsAt is null || EndsAt > now);
}

/// <summary>
/// Table: approval_requests. The payload is frozen once created — to change it you cancel
/// and raise a new request, so a reviewer always approves what they read (§9.2).
/// </summary>
public class ApprovalRequest : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>One of <see cref="ApprovalActionTypes"/>; dispatch is by registered handler only.</summary>
    public string ActionType { get; set; } = null!;

    public string TargetType { get; set; } = null!;

    public Guid? TargetId { get; set; }

    public string Payload { get; set; } = "{}";

    public string Reason { get; set; } = null!;

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public Guid RequestedBy { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? ExecutedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public ICollection<ApprovalDecision> Decisions { get; set; } = new List<ApprovalDecision>();

    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>The requester can never be a reviewer, whatever roles they hold (§9.2).</summary>
    public void EnsureReviewerAllowed(Guid reviewerId)
    {
        if (reviewerId == RequestedBy)
        {
            throw DomainException.Forbidden(
                "申請人不得覆核自己提出的案件。", ErrorCodes.SelfApprovalForbidden);
        }
    }

    public void EnsureExecutable(DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Approved)
        {
            throw DomainException.Conflict(
                ErrorCodes.ApprovalNotApproved,
                $"案件目前為 {EnumNaming.ToDbValue(Status)}，無法執行。");
        }

        if (IsExpiredAt(now))
        {
            throw DomainException.Conflict(ErrorCodes.ApprovalExpired, "此覆核案件已逾期。");
        }
    }
}

/// <summary>Table: approval_decisions — append-only.</summary>
public class ApprovalDecision : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ApprovalRequestId { get; set; }

    public ApprovalRequest? ApprovalRequest { get; set; }

    public Guid ReviewerId { get; set; }

    public ApprovalDecisionKind Decision { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset DecidedAt { get; set; }
}

/// <summary>
/// Table: audit_logs — append-only with permanent retention. There is deliberately no
/// purge job and no delete API (§0.2).
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorRole { get; set; }

    public string Action { get; set; } = null!;

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? BeforeData { get; set; }

    public string? AfterData { get; set; }

    public string? Reason { get; set; }

    public string? RequestId { get; set; }

    public IPAddress? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string Metadata { get; set; } = "{}";
}

/// <summary>Table: idempotency_records (§8.2).</summary>
public class IdempotencyRecord : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string HttpMethod { get; set; } = null!;

    public string RequestPath { get; set; } = null!;

    public string IdempotencyKey { get; set; } = null!;

    /// <summary>Hex SHA-256 of the request body; a different hash under the same key is a 409.</summary>
    public string RequestHash { get; set; } = null!;

    public IdempotencyStatus Status { get; set; } = IdempotencyStatus.Processing;

    public int? ResponseStatus { get; set; }

    public string? ResponseBody { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsStaleProcessing(DateTimeOffset now, TimeSpan timeout) =>
        Status == IdempotencyStatus.Processing && now - CreatedAt > timeout;
}

/// <summary>Table: outbox_messages — written in the business transaction, dispatched after commit.</summary>
public class OutboxMessage : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Topic { get; set; } = null!;

    public string AggregateType { get; set; } = null!;

    public Guid AggregateId { get; set; }

    public string Payload { get; set; } = "{}";

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public bool IsPending => ProcessedAt is null;

    /// <summary>Exponential backoff, capped so a stuck message does not drift days ahead.</summary>
    public TimeSpan NextBackoff() =>
        TimeSpan.FromSeconds(Math.Min(600, Math.Pow(2, Math.Min(AttemptCount, 10))));
}

/// <summary>Table: scheduled_jobs — definition plus the worker lease.</summary>
public class ScheduledJob : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string JobKey { get; set; } = null!;

    public string JobType { get; set; } = null!;

    public string? CronExpression { get; set; }

    public string Payload { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? NextRunAt { get; set; }

    public string? LockedBy { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsLeased(DateTimeOffset now) => LockedUntil is not null && LockedUntil > now;

    public bool IsDue(DateTimeOffset now) =>
        IsEnabled && NextRunAt is not null && NextRunAt <= now && !IsLeased(now);
}

/// <summary>Table: job_runs — append-only.</summary>
public class JobRun : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ScheduledJobId { get; set; }

    public ScheduledJob? ScheduledJob { get; set; }

    public JobRunStatus Status { get; set; } = JobRunStatus.Running;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int AttemptNo { get; set; } = 1;

    public string ResultPayload { get; set; } = "{}";

    public string? ErrorMessage { get; set; }

    public string WorkerId { get; set; } = null!;
}
