using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Events;
using GongWei.Domain.Identity;
using GongWei.Domain.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class EventRoomConfiguration : IEntityTypeConfiguration<EventRoom>
{
    public void Configure(EntityTypeBuilder<EventRoom> b)
    {
        b.ToTable("event_rooms", t =>
        {
            t.HasCheckConstraint("ck_er_event_type",
                "event_type IN ('main', 'social', 'investigation', 'limited', 'private', 'admin')");
            t.HasCheckConstraint("ck_er_status",
                "status IN ('draft', 'scheduled', 'open', 'locked', 'settled', 'cancelled')");
            t.HasCheckConstraint("ck_er_visibility", "visibility IN ('public', 'invited', 'private')");
            t.HasCheckConstraint("ck_er_participant_limit",
                "participant_limit IS NULL OR participant_limit > 0");
            t.HasCheckConstraint("ck_er_rules_snapshot", "jsonb_typeof(rules_snapshot) = 'object'");
            t.HasCheckConstraint("ck_er_window",
                "deadline_at IS NULL OR opens_at IS NULL OR deadline_at > opens_at");
            t.HasCheckConstraint("ck_er_settled_at",
                "(status = 'settled' AND settled_at IS NOT NULL) OR status <> 'settled'");
            t.HasCheckConstraint("ck_er_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.BodyMarkdown).HasDefaultValue(string.Empty);
        b.Property(x => x.EventType).HasMaxLength(30);
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(EventRoomStatus.Draft);
        b.Property(x => x.Visibility).HasMaxLength(20).HasDefaultValue(EventVisibility.Public);
        b.Property(x => x.RulesVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.RulesSnapshot).JsonObject();
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne<WorldLocation>().WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.Status, x.OpensAt, x.DeadlineAt })
            .HasDatabaseName("ix_event_rooms_player_list");
    }
}

public class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> b)
    {
        b.ToTable("event_participants", t =>
        {
            t.HasCheckConstraint("ck_ep_status",
                "status IN ('invited', 'joined', 'left', 'removed', 'completed')");
            t.HasCheckConstraint("ck_ep_metadata", "jsonb_typeof(metadata) = 'object'");
        });

        b.HasKey(x => new { x.EventRoomId, x.CharacterId });
        b.Property(x => x.ParticipantRole).HasMaxLength(40).HasDefaultValue("participant");
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(ParticipantStatus.Joined);
        b.Property(x => x.Metadata).JsonObject();

        b.HasOne(x => x.EventRoom).WithMany(e => e.Participants)
            .HasForeignKey(x => x.EventRoomId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CharacterId, x.Status })
            .HasDatabaseName("ix_event_participants_character");
    }
}

public class EventPostConfiguration : IEntityTypeConfiguration<EventPost>
{
    public void Configure(EntityTypeBuilder<EventPost> b)
    {
        b.ToTable("event_posts", t =>
        {
            t.HasCheckConstraint("ck_epost_status",
                "status IN ('draft', 'submitted', 'under_review', 'approved', " +
                "'needs_revision', 'rejected', 'withdrawn', 'moderated')");
            t.HasCheckConstraint("ck_epost_body_len", "char_length(body_markdown) <= 10000");
            // A draft may be blank; anything past draft must have real content.
            t.HasCheckConstraint("ck_epost_body_not_blank",
                "status = 'draft' OR char_length(btrim(body_markdown)) > 0");
            t.HasCheckConstraint("ck_epost_submitted_at",
                "status NOT IN ('submitted', 'under_review', 'approved', 'rejected', 'needs_revision') " +
                "OR submitted_at IS NOT NULL");
            // Only an approved post gets a published_at, which is what puts it in the feed.
            t.HasCheckConstraint("ck_epost_approved_published",
                "status <> 'approved' OR (reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL " +
                "AND published_at IS NOT NULL)");
            t.HasCheckConstraint("ck_epost_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.BodyMarkdown).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(EventPostStatus.Draft);
        b.Property(x => x.ClientRequestId).HasMaxLength(80);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Property(x => x.ModerationNote).HasMaxLength(500);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.EventRoom).WithMany(e => e.Posts)
            .HasForeignKey(x => x.EventRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ModeratedBy).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.EventRoomId, x.CharacterId, x.ClientRequestId }).IsUnique();
        // The public feed only ever reads approved posts.
        b.HasIndex(x => new { x.EventRoomId, x.PublishedAt, x.Id })
            .HasDatabaseName("ix_event_posts_room_feed")
            .HasFilter("status = 'approved'");
        b.HasIndex(x => new { x.Status, x.SubmittedAt })
            .HasDatabaseName("ix_event_posts_review_queue")
            .HasFilter("status IN ('submitted', 'under_review')");
    }
}

public class EventPostRevisionConfiguration : IEntityTypeConfiguration<EventPostRevision>
{
    public void Configure(EntityTypeBuilder<EventPostRevision> b)
    {
        b.ToTable("event_post_revisions", t =>
        {
            t.HasCheckConstraint("ck_epr_revision_no", "revision_no > 0");
            t.HasCheckConstraint("ck_epr_revision_kind",
                "revision_kind IN ('draft_save', 'submit', 'revision_request', 'approval', 'moderation')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.BodyMarkdown).IsRequired();
        b.Property(x => x.RevisionKind).HasMaxLength(20).HasDefaultValue(EventPostRevisionKind.DraftSave);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Post).WithMany()
            .HasForeignKey(x => x.EventPostId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EventPostId, x.RevisionNo }).IsUnique();
    }
}

public class EventResultConfiguration : IEntityTypeConfiguration<EventResult>
{
    public void Configure(EntityTypeBuilder<EventResult> b)
    {
        b.ToTable("event_results", t =>
        {
            t.HasCheckConstraint("ck_eres_private_payload", "jsonb_typeof(private_payload) = 'object'");
            t.HasCheckConstraint("ck_eres_rewards_payload", "jsonb_typeof(rewards_payload) = 'object'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.OutcomeCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.PublicSummary).HasMaxLength(2000).IsRequired();
        b.Property(x => x.PrivatePayload).JsonObject();
        b.Property(x => x.RewardsPayload).JsonObject();
        b.Property(x => x.RulesVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.EventRoom).WithMany()
            .HasForeignKey(x => x.EventRoomId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SettledBy).OnDelete(DeleteBehavior.Restrict);

        // NULLS NOT DISTINCT makes the single NULL-character global row unique too.
        b.HasIndex(x => new { x.EventRoomId, x.CharacterId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}

public class ExternalPlaySubmissionConfiguration : IEntityTypeConfiguration<ExternalPlaySubmission>
{
    public void Configure(EntityTypeBuilder<ExternalPlaySubmission> b)
    {
        b.ToTable("external_play_submissions", t =>
        {
            t.HasCheckConstraint("ck_eps_source_type", "source_type IN ('line_group', 'other')");
            t.HasCheckConstraint("ck_eps_status",
                "status IN ('submitted', 'under_review', 'approved', 'rejected', 'cancelled')");
            t.HasCheckConstraint("ck_eps_evidence_urls", "jsonb_typeof(evidence_urls) = 'array'");
            t.HasCheckConstraint("ck_eps_involved", "jsonb_typeof(involved_character_ids) = 'array'");
            t.HasCheckConstraint("ck_eps_summary_len", "char_length(btrim(summary)) BETWEEN 1 AND 4000");
            t.HasCheckConstraint("ck_eps_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.SourceType).HasMaxLength(20).HasDefaultValue(ExternalPlaySourceType.LineGroup);
        b.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
        b.Property(x => x.EvidenceUrls).JsonArray();
        b.Property(x => x.InvolvedCharacterIds).JsonArray();
        b.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(ExternalPlayStatus.Submitted);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.SubmittedByCharacter).WithMany()
            .HasForeignKey(x => x.SubmittedByCharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_external_play_review_queue")
            .HasFilter("status IN ('submitted', 'under_review')");
    }
}
