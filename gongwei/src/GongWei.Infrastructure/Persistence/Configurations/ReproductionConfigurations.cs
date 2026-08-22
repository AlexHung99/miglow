using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Domain.Intrigue;
using GongWei.Domain.Reproduction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class ReproductionControlConfiguration : IEntityTypeConfiguration<ReproductionControl>
{
    public void Configure(EntityTypeBuilder<ReproductionControl> b)
    {
        b.ToTable("reproduction_control", t =>
        {
            t.HasCheckConstraint("ck_rc_singleton", "singleton_id = 1");
            t.HasCheckConstraint("ck_rc_conception_rate",
                "conception_rate_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_rc_duration",
                "pregnancy_duration_days BETWEEN 1 AND 365");
            t.HasCheckConstraint("ck_rc_miscarriage_mode",
                "miscarriage_mode IN ('disabled', 'event_only', 'threshold', 'daily_probability')");
            t.HasCheckConstraint("ck_rc_miscarriage_rules", "jsonb_typeof(miscarriage_rules) = 'object'");
            t.HasCheckConstraint("ck_rc_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("singleton_id").ValueGeneratedNever().HasDefaultValue((short)1);
        b.Property(x => x.IsOpen).HasDefaultValue(true);
        b.Property(x => x.ClosedReason).HasMaxLength(500);
        b.Property(x => x.ConceptionRatePercent)
            .HasDefaultValue(ReproductionControl.DefaultConceptionRatePercent);
        b.Property(x => x.PregnancyDurationDays)
            .HasDefaultValue(ReproductionControl.DefaultPregnancyDurationDays);
        b.Property(x => x.MiscarriageMode).HasMaxLength(30).HasDefaultValue(MiscarriageMode.EventOnly);
        b.Property(x => x.MiscarriageRules)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("""'{"baseRatePercent":0}'::jsonb""");
        b.Property(x => x.RulesVersion).HasMaxLength(40).HasDefaultValue("reproduction-1");
        b.DatabaseManagedVersion();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

public class HeirWaitPoolEntryConfiguration : IEntityTypeConfiguration<HeirWaitPoolEntry>
{
    public void Configure(EntityTypeBuilder<HeirWaitPoolEntry> b)
    {
        b.ToTable("heir_wait_pool_entries", t =>
        {
            t.HasCheckConstraint("ck_hwp_status",
                "status IN ('waiting', 'drawn', 'withdrawn', 'suspended')");
            t.HasCheckConstraint("ck_hwp_resolved_pair",
                "(status = 'waiting' AND resolved_at IS NULL) OR " +
                "(status <> 'waiting' AND resolved_at IS NOT NULL)");
            t.HasCheckConstraint("ck_hwp_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(WaitPoolStatus.Waiting);
        b.Property(x => x.EnteredAt).CreatedNow();
        b.Property(x => x.ResolvedReason).HasMaxLength(500);
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CharacterId)
            .IsUnique()
            .HasDatabaseName("ux_heir_wait_pool_one_waiting_per_character")
            .HasFilter("status = 'waiting'");
        b.HasIndex(x => new { x.EnteredAt, x.Id })
            .HasDatabaseName("ix_heir_wait_pool_draw_candidates")
            .HasFilter("status = 'waiting'");
    }
}

public class AudienceRequestConfiguration : IEntityTypeConfiguration<AudienceRequest>
{
    public void Configure(EntityTypeBuilder<AudienceRequest> b)
    {
        b.ToTable("audience_requests", t =>
        {
            t.HasCheckConstraint("ck_ar_type", "audience_type IN ('meal', 'bedchamber')");
            t.HasCheckConstraint("ck_ar_status",
                "status IN ('submitted', 'approved', 'rejected', 'resolved', 'cancelled')");
            t.HasCheckConstraint("ck_ar_qualification",
                "jsonb_typeof(qualification_snapshot) = 'object'");
            t.HasCheckConstraint("ck_ar_result_payload", "jsonb_typeof(result_payload) = 'object'");
            t.HasCheckConstraint("ck_ar_resolved_pair",
                "(status IN ('resolved', 'rejected', 'cancelled') AND resolved_at IS NOT NULL) OR " +
                "status IN ('submitted', 'approved')");
            t.HasCheckConstraint("ck_ar_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.AudienceType).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(AudienceRequestStatus.Submitted);
        b.Property(x => x.QualificationSnapshot).RequiredJson();
        b.Property(x => x.RequestedAt).CreatedNow();
        b.Property(x => x.ResultCode).HasMaxLength(80);
        b.Property(x => x.ResultPayload).JsonObject();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CharacterId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.CharacterId, x.RequestedAt })
            .HasDatabaseName("ix_audience_requests_character");
    }
}

public class PregnancyConfiguration : IEntityTypeConfiguration<Pregnancy>
{
    public void Configure(EntityTypeBuilder<Pregnancy> b)
    {
        b.ToTable("pregnancies", t =>
        {
            t.HasCheckConstraint("ck_preg_status",
                "status IN ('ongoing', 'miscarried', 'completed', 'cancelled')");
            t.HasCheckConstraint("ck_preg_rate", "conception_rate_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_preg_roll", "conception_roll BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_preg_rules_snapshot", "jsonb_typeof(rules_snapshot) = 'object'");
            t.HasCheckConstraint("ck_preg_due", "due_at > conceived_at");
            t.HasCheckConstraint("ck_preg_slot_reserved", "slot_reserved_at >= conceived_at");
            // A pregnancy that is no longer ongoing must have released its heir slot (§6.3).
            t.HasCheckConstraint("ck_preg_slot_release",
                "(status = 'ongoing' AND slot_released_at IS NULL) OR " +
                "(status <> 'ongoing' AND slot_released_at IS NOT NULL)");
            // Miscarriage always states a code and a real reason (§6.3).
            t.HasCheckConstraint("ck_preg_miscarriage_reason",
                "status <> 'miscarried' OR (resolution_code IS NOT NULL AND " +
                "char_length(btrim(resolution_reason)) >= 5)");
            t.HasCheckConstraint("ck_preg_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(PregnancyStatus.Ongoing);
        b.Property(x => x.RulesVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.RulesSnapshot).RequiredJson();
        b.Property(x => x.ResolutionCode).HasMaxLength(80);
        b.Property(x => x.ResolutionReason).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Mother).WithMany()
            .HasForeignKey(x => x.MotherCharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AudienceRequest).WithMany()
            .HasForeignKey(x => x.AudienceRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedBy).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.AudienceRequestId).IsUnique();
        b.HasIndex(x => x.MotherCharacterId)
            .IsUnique()
            .HasDatabaseName("ux_pregnancies_one_ongoing_per_mother")
            .HasFilter("status = 'ongoing'");
        b.HasIndex(x => x.DueAt).HasDatabaseName("ix_pregnancies_due").HasFilter("status = 'ongoing'");
    }
}

public class BirthConfiguration : IEntityTypeConfiguration<Birth>
{
    public void Configure(EntityTypeBuilder<Birth> b)
    {
        b.ToTable("births", t =>
            t.HasCheckConstraint("ck_births_candidate_count", "candidate_count > 0"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.CandidateSetHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.RandomAlgorithm).HasMaxLength(80).IsRequired();
        b.Property(x => x.RandomProofHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.RulesVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Pregnancy).WithMany()
            .HasForeignKey(x => x.PregnancyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HeirWaitPoolEntry>().WithMany()
            .HasForeignKey(x => x.WaitPoolEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.ChildCharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DrawnBy).OnDelete(DeleteBehavior.SetNull);

        // A pregnancy, a pool entry and a child each appear in at most one birth.
        b.HasIndex(x => x.PregnancyId).IsUnique();
        b.HasIndex(x => x.WaitPoolEntryId).IsUnique();
        b.HasIndex(x => x.ChildCharacterId).IsUnique();
    }
}

public class OffspringLinkConfiguration : IEntityTypeConfiguration<OffspringLink>
{
    public void Configure(EntityTypeBuilder<OffspringLink> b)
    {
        b.ToTable("offspring_links", t =>
        {
            t.HasCheckConstraint("ck_ol_parent_type", "parent_type IN ('mother', 'father')");
            t.HasCheckConstraint("ck_ol_parent_xor",
                "(parent_character_id IS NOT NULL)::integer + (parent_npc_code IS NOT NULL)::integer = 1");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.ParentType).HasMaxLength(20);
        b.Property(x => x.ParentNpcCode).HasMaxLength(80);
        b.Property(x => x.IsPublic).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.ChildCharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.ParentCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ChildCharacterId, x.ParentType, x.ParentCharacterId, x.ParentNpcCode })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}

public class IntrigueActionConfiguration : IEntityTypeConfiguration<IntrigueAction>
{
    public void Configure(EntityTypeBuilder<IntrigueAction> b)
    {
        b.ToTable("intrigue_actions", t =>
        {
            t.HasCheckConstraint("ck_ia_action_type",
                "action_type IN ('poison', 'investigate', 'countermeasure')");
            t.HasCheckConstraint("ck_ia_status",
                "status IN ('submitted', 'processing', 'resolved', 'failed', 'cancelled')");
            t.HasCheckConstraint("ck_ia_input_payload", "jsonb_typeof(input_payload) = 'object'");
            t.HasCheckConstraint("ck_ia_secret_result", "jsonb_typeof(secret_result) = 'object'");
            t.HasCheckConstraint("ck_ia_public_result", "jsonb_typeof(public_result) = 'object'");
            t.HasCheckConstraint("ck_ia_not_self", "actor_character_id <> target_character_id");
            t.HasCheckConstraint("ck_ia_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.ActionType).HasMaxLength(30);
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(IntrigueStatus.Submitted);
        b.Property(x => x.InputPayload).JsonObject();
        b.Property(x => x.SecretResult).JsonObject();
        b.Property(x => x.PublicResult).JsonObject();
        b.Property(x => x.RulesVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.SubmittedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Actor).WithMany()
            .HasForeignKey(x => x.ActorCharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Target).WithMany()
            .HasForeignKey(x => x.TargetCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ActorCharacterId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.ResolveAfter)
            .HasDatabaseName("ix_intrigue_actions_pending")
            .HasFilter("status IN ('submitted', 'processing')");
    }
}

public class StatusEffectConfiguration : IEntityTypeConfiguration<StatusEffect>
{
    public void Configure(EntityTypeBuilder<StatusEffect> b)
    {
        b.ToTable("status_effects", t =>
        {
            t.HasCheckConstraint("ck_se_visibility", "visibility IN ('private', 'public', 'admin_only')");
            t.HasCheckConstraint("ck_se_severity", "severity BETWEEN 1 AND 10");
            t.HasCheckConstraint("ck_se_payload", "jsonb_typeof(payload) = 'object'");
            t.HasCheckConstraint("ck_se_expiry", "expires_at IS NULL OR expires_at > starts_at");
            t.HasCheckConstraint("ck_se_resolved", "resolved_at IS NULL OR resolved_at >= starts_at");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.EffectCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.Visibility).HasMaxLength(20).HasDefaultValue(EffectVisibility.Private);
        b.Property(x => x.Severity).HasDefaultValue((short)1);
        b.Property(x => x.Payload).JsonObject();
        b.Property(x => x.SourceType).HasMaxLength(60);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CharacterId, x.EffectCode })
            .HasDatabaseName("ix_status_effects_active")
            .HasFilter("resolved_at IS NULL");
    }
}

public class DeathConfiguration : IEntityTypeConfiguration<Death>
{
    public void Configure(EntityTypeBuilder<Death> b)
    {
        b.ToTable("deaths", t =>
            t.HasCheckConstraint("ck_deaths_private_details", "jsonb_typeof(private_details) = 'object'"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.CauseCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.PublicCause).HasMaxLength(1000).IsRequired();
        b.Property(x => x.PrivateDetails).JsonObject();
        b.Property(x => x.SourceType).HasMaxLength(60);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RuledBy).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Domain.Operations.ApprovalRequest>().WithMany()
            .HasForeignKey(x => x.ApprovalRequestId)
            .HasConstraintName("fk_deaths_approval_request")
            .OnDelete(DeleteBehavior.SetNull);

        // A character dies exactly once.
        b.HasIndex(x => x.CharacterId).IsUnique();
    }
}
