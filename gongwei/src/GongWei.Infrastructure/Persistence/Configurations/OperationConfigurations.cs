using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications", t =>
            t.HasCheckConstraint("ck_notif_payload", "jsonb_typeof(payload) = 'object'"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.NotificationType).HasMaxLength(60).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Route).HasMaxLength(300);
        b.Property(x => x.Payload).JsonObject();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("ix_notifications_user_unread")
            .HasFilter("read_at IS NULL");
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.ToTable("announcements", t =>
        {
            t.HasCheckConstraint("ck_ann_severity", "severity IN ('info', 'warning', 'critical')");
            t.HasCheckConstraint("ck_ann_audience", "audience IN ('all', 'players', 'admins')");
            t.HasCheckConstraint("ck_ann_window", "ends_at IS NULL OR ends_at > starts_at");
            t.HasCheckConstraint("ck_ann_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.BodyMarkdown).IsRequired();
        b.Property(x => x.Severity).HasMaxLength(20).HasDefaultValue(AnnouncementSeverity.Info);
        b.Property(x => x.Audience).HasMaxLength(20).HasDefaultValue(AnnouncementAudience.All);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.StartsAt, x.EndsAt }).HasDatabaseName("ix_announcements_active");
    }
}

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> b)
    {
        b.ToTable("approval_requests", t =>
        {
            t.HasCheckConstraint("ck_apr_status",
                "status IN ('pending', 'approved', 'rejected', 'expired', 'executed', 'cancelled')");
            t.HasCheckConstraint("ck_apr_payload", "jsonb_typeof(payload) = 'object'");
            t.HasCheckConstraint("ck_apr_expiry", "expires_at > requested_at");
            t.HasCheckConstraint("ck_apr_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        // Free text in the schema, but dispatch only matches ApprovalActionTypes (§12).
        b.Property(x => x.ActionType).HasMaxLength(80).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(60).IsRequired();
        b.Property(x => x.Payload).RequiredJson();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(ApprovalStatus.Pending);
        b.Property(x => x.RequestedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.RequestedAt)
            .HasDatabaseName("ix_approval_requests_pending")
            .HasFilter("status = 'pending'");
    }
}

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> b)
    {
        b.ToTable("approval_decisions", t =>
            t.HasCheckConstraint("ck_apd_decision", "decision IN ('approve', 'reject')"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Decision).HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.DecidedAt).CreatedNow();

        b.HasOne(x => x.ApprovalRequest).WithMany(r => r.Decisions)
            .HasForeignKey(x => x.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);

        // One decision per reviewer per request; the no-self-review rule is enforced in
        // the domain and again by a trigger.
        b.HasIndex(x => new { x.ApprovalRequestId, x.ReviewerId }).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs", t =>
            t.HasCheckConstraint("ck_audit_metadata", "jsonb_typeof(metadata) = 'object'"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();
        b.Property(x => x.OccurredAt).CreatedNow();
        b.Property(x => x.ActorRole).HasMaxLength(40);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(60);
        b.Property(x => x.BeforeData).NullableJson();
        b.Property(x => x.AfterData).NullableJson();
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.RequestId).HasMaxLength(80);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.Metadata).JsonObject();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt })
            .HasDatabaseName("ix_audit_logs_target");
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAt }).HasDatabaseName("ix_audit_logs_actor");
    }
}

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records", t =>
        {
            t.HasCheckConstraint("ck_idem_status", "status IN ('processing', 'completed', 'failed')");
            t.HasCheckConstraint("ck_idem_response_status",
                "response_status IS NULL OR response_status BETWEEN 100 AND 599");
            t.HasCheckConstraint("ck_idem_expiry", "expires_at > created_at");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
        b.Property(x => x.RequestPath).HasMaxLength(300).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(IdempotencyStatus.Processing);
        b.Property(x => x.ResponseBody).NullableJson();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Key is scoped to user + method + path (§8.2).
        b.HasIndex(x => new { x.UserId, x.HttpMethod, x.RequestPath, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_idempotency_records_expiry");
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages", t =>
        {
            t.HasCheckConstraint("ck_outbox_payload", "jsonb_typeof(payload) = 'object'");
            t.HasCheckConstraint("ck_outbox_attempts", "attempt_count >= 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Topic).HasMaxLength(100).IsRequired();
        b.Property(x => x.AggregateType).HasMaxLength(60).IsRequired();
        b.Property(x => x.Payload).RequiredJson();
        b.Property(x => x.OccurredAt).CreatedNow();
        b.Property(x => x.AvailableAt).CreatedNow();
        b.Property(x => x.AttemptCount).HasDefaultValue(0);
        b.Property(x => x.LastError).HasMaxLength(2000);

        b.Ignore(x => x.IsPending);

        // Drives FOR UPDATE SKIP LOCKED polling in the worker (§10).
        b.HasIndex(x => new { x.AvailableAt, x.OccurredAt })
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_at IS NULL");
    }
}

public class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
{
    public void Configure(EntityTypeBuilder<ScheduledJob> b)
    {
        b.ToTable("scheduled_jobs", t =>
        {
            t.HasCheckConstraint("ck_sj_payload", "jsonb_typeof(payload) = 'object'");
            t.HasCheckConstraint("ck_sj_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.JobKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.JobType).HasMaxLength(80).IsRequired();
        b.Property(x => x.CronExpression).HasMaxLength(100);
        b.Property(x => x.Payload).JsonObject();
        b.Property(x => x.IsEnabled).HasDefaultValue(true);
        b.Property(x => x.LockedBy).HasMaxLength(100);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => x.JobKey).IsUnique();
        b.HasIndex(x => x.NextRunAt)
            .HasDatabaseName("ix_scheduled_jobs_due")
            .HasFilter("is_enabled = true");
    }
}

public class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> b)
    {
        b.ToTable("job_runs", t =>
        {
            t.HasCheckConstraint("ck_jr_status",
                "status IN ('running', 'succeeded', 'failed', 'cancelled')");
            t.HasCheckConstraint("ck_jr_attempt_no", "attempt_no > 0");
            t.HasCheckConstraint("ck_jr_result_payload", "jsonb_typeof(result_payload) = 'object'");
            t.HasCheckConstraint("ck_jr_finished_pair",
                "(status = 'running' AND finished_at IS NULL) OR " +
                "(status <> 'running' AND finished_at IS NOT NULL)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.StartedAt).CreatedNow();
        b.Property(x => x.AttemptNo).HasDefaultValue(1);
        b.Property(x => x.ResultPayload).JsonObject();
        b.Property(x => x.ErrorMessage).HasMaxLength(4000);
        b.Property(x => x.WorkerId).HasMaxLength(100).IsRequired();

        b.HasOne(x => x.ScheduledJob).WithMany()
            .HasForeignKey(x => x.ScheduledJobId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ScheduledJobId, x.StartedAt }).HasDatabaseName("ix_job_runs_job");
    }
}
