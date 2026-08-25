using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class RankConfiguration : IEntityTypeConfiguration<Rank>
{
    public void Configure(EntityTypeBuilder<Rank> b)
    {
        b.ToTable("ranks", t =>
        {
            t.HasCheckConstraint("ck_ranks_role", "applies_to_role IN ('consort', 'prince', 'princess')");
            t.HasCheckConstraint("ck_ranks_ordinal", "ordinal >= 0");
            t.HasCheckConstraint("ck_ranks_prestige", "prestige_required >= 0");
            t.HasCheckConstraint("ck_ranks_monthly_stipend", "monthly_stipend >= 0");
            t.HasCheckConstraint("ck_ranks_annual_stipend", "source_annual_stipend >= 0");
            t.HasCheckConstraint("ck_ranks_capacity", "capacity IS NULL OR capacity > 0");
            t.HasCheckConstraint("ck_ranks_initial_stats",
                "initial_stats IS NULL OR jsonb_typeof(initial_stats) = 'object'");
            t.HasCheckConstraint("ck_ranks_promotion_rules", "jsonb_typeof(promotion_rules) = 'object'");
            t.HasCheckConstraint("ck_ranks_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        b.Property(x => x.AppliesToRole).HasMaxLength(20);
        b.Property(x => x.GradeCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.PrestigeRequired).HasDefaultValue(0L);
        b.Property(x => x.MonthlyStipend).HasDefaultValue(0L);
        b.Property(x => x.SourceAnnualStipend).HasDefaultValue(0L);
        b.Property(x => x.IsLead).HasDefaultValue(false);
        b.Property(x => x.IsApplicationOption).HasDefaultValue(false);
        b.Property(x => x.InitialStats).NullableJson();
        b.Property(x => x.PromotionRules).JsonObject();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => x.Code).IsUnique();
        // One grade holds several 位號, so display_name — not ordinal — completes the key.
        b.HasIndex(x => new { x.AppliesToRole, x.DisplayName }).IsUnique();
        b.HasIndex(x => new { x.AppliesToRole, x.Ordinal, x.DisplayName })
            .HasDatabaseName("ix_ranks_role_grade");
    }
}

public class CharacterTitleDefinitionConfiguration : IEntityTypeConfiguration<CharacterTitleDefinition>
{
    public void Configure(EntityTypeBuilder<CharacterTitleDefinition> b)
    {
        b.ToTable("character_title_definitions", t =>
        {
            t.HasCheckConstraint("ck_ctd_category",
                "category IN ('rank', 'achievement', 'story', 'honorary', 'secret')");
            t.HasCheckConstraint("ck_ctd_role",
                "applies_to_role IS NULL OR applies_to_role IN ('consort', 'prince', 'princess')");
            t.HasCheckConstraint("ck_ctd_visibility",
                "visibility IN ('public', 'owner_only', 'admin_only')");
            t.HasCheckConstraint("ck_ctd_display_name_len",
                "char_length(btrim(display_name)) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_ctd_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Category).HasMaxLength(30);
        b.Property(x => x.AppliesToRole).HasMaxLength(20);
        b.Property(x => x.Visibility).HasMaxLength(20).HasDefaultValue(TitleVisibility.Public);
        b.Property(x => x.StyleToken).HasMaxLength(50);
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class ResidenceConfiguration : IEntityTypeConfiguration<Residence>
{
    public void Configure(EntityTypeBuilder<Residence> b)
    {
        b.ToTable("residences", t =>
        {
            t.HasCheckConstraint("ck_residences_map_x", "map_x IS NULL OR map_x BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_residences_map_y", "map_y IS NULL OR map_y BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_residences_capacity", "capacity IS NULL OR capacity > 0");
            t.HasCheckConstraint("ck_residences_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.MapX).HasColumnType("numeric(5,2)");
        b.Property(x => x.MapY).HasColumnType("numeric(5,2)");
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class CharacterApplicationConfiguration : IEntityTypeConfiguration<CharacterApplication>
{
    public void Configure(EntityTypeBuilder<CharacterApplication> b)
    {
        b.ToTable("character_applications", t =>
        {
            t.HasCheckConstraint("ck_ca_role", "role IN ('consort', 'prince', 'princess')");
            t.HasCheckConstraint("ck_ca_sex", "sex IN ('female', 'male')");
            t.HasCheckConstraint("ck_ca_status",
                "status IN ('draft', 'submitted', 'needs_revision', 'approved', 'rejected', 'cancelled')");
            t.HasCheckConstraint("ck_ca_form_data", "jsonb_typeof(form_data) = 'object'");
            // Role and sex can never disagree — sex is derived, never supplied (§13.1).
            t.HasCheckConstraint("ck_ca_role_sex",
                "(role = 'prince' AND sex = 'male') OR (role IN ('consort', 'princess') AND sex = 'female')");
            // Draft may be incomplete; only identity, age and portrait are required on submit.
            t.HasCheckConstraint("ck_ca_given_name",
                "status = 'draft' OR char_length(btrim(given_name)) BETWEEN 1 AND 30");
            t.HasCheckConstraint("ck_ca_portrait_xor",
                "status = 'draft' OR ((portrait_id IS NOT NULL)::integer + " +
                "(player_portrait_submission_id IS NOT NULL)::integer = 1)");
            t.HasCheckConstraint("ck_ca_age_and_family",
                "status = 'draft' OR " +
                "(role = 'consort' AND age BETWEEN 15 AND 18 AND char_length(btrim(family_name)) > 0) OR " +
                "(role IN ('prince', 'princess') AND age = 0 AND family_name = '蕭')");
            t.HasCheckConstraint("ck_ca_draft_not_submitted",
                "(status = 'draft' AND submitted_at IS NULL) OR status <> 'draft'");
            t.HasCheckConstraint("ck_ca_approved_reviewed",
                "(status = 'approved' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL) " +
                "OR status <> 'approved'");
            t.HasCheckConstraint("ck_ca_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Role).HasMaxLength(20);
        b.Property(x => x.Sex).HasMaxLength(10);
        b.Property(x => x.FamilyName).HasMaxLength(20).HasDefaultValue(string.Empty);
        b.Property(x => x.GivenName).HasMaxLength(30).HasDefaultValue(string.Empty);
        b.Property(x => x.CourtesyName).HasMaxLength(30);
        b.Property(x => x.BirthDateLabel).HasMaxLength(30);
        b.Property(x => x.Appearance).HasMaxLength(3000).HasDefaultValue(string.Empty);
        b.Property(x => x.Biography).HasMaxLength(2000).HasDefaultValue(string.Empty);
        b.Property(x => x.Personality).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Strengths).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Weaknesses).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Likes).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Dislikes).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(ApplicationStatus.Draft);
        b.Property(x => x.FormData).JsonObject();
        b.Property(x => x.ReviewNote).HasMaxLength(2000);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Portrait).WithMany()
            .HasForeignKey(x => x.PortraitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PlayerPortraitSubmission).WithMany()
            .HasForeignKey(x => x.PlayerPortraitSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.CreatedCharacterId)
            .HasConstraintName("fk_character_applications_created_character")
            .OnDelete(DeleteBehavior.SetNull);

        // One open application per account (§5.1).
        b.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("ux_character_applications_one_open_per_user")
            .HasFilter("status IN ('draft', 'submitted', 'needs_revision')");
        b.HasIndex(x => new { x.Status, x.SubmittedAt })
            .HasDatabaseName("ix_character_applications_review_queue")
            .HasFilter("status IN ('submitted', 'needs_revision')");
    }
}

public class CharacterApplicationRevisionConfiguration : IEntityTypeConfiguration<CharacterApplicationRevision>
{
    public void Configure(EntityTypeBuilder<CharacterApplicationRevision> b)
    {
        b.ToTable("character_application_revisions", t =>
        {
            t.HasCheckConstraint("ck_car_revision_no", "revision_no > 0");
            t.HasCheckConstraint("ck_car_snapshot", "jsonb_typeof(snapshot) = 'object'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Snapshot).RequiredJson();
        b.Property(x => x.ChangeReason).HasMaxLength(500);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Application).WithMany(a => a.Revisions)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ApplicationId, x.RevisionNo }).IsUnique();
    }
}

public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> b)
    {
        b.ToTable("characters", t =>
        {
            t.HasCheckConstraint("ck_characters_role", "role IN ('consort', 'prince', 'princess')");
            t.HasCheckConstraint("ck_characters_sex", "sex IN ('female', 'male')");
            t.HasCheckConstraint("ck_characters_status",
                "status IN ('waiting_birth', 'active', 'paused', 'dead', 'suspended', 'archived')");
            t.HasCheckConstraint("ck_characters_role_sex",
                "(role = 'prince' AND sex = 'male') OR (role IN ('consort', 'princess') AND sex = 'female')");
            t.HasCheckConstraint("ck_characters_portrait_xor",
                "(portrait_id IS NOT NULL)::integer + (player_portrait_submission_id IS NOT NULL)::integer = 1");
            t.HasCheckConstraint("ck_characters_waiting_birth_role",
                "(status = 'waiting_birth' AND role IN ('prince', 'princess')) OR status <> 'waiting_birth'");
            t.HasCheckConstraint("ck_characters_dead_at",
                "(status = 'dead' AND died_at IS NOT NULL) OR status <> 'dead'");
            t.HasCheckConstraint("ck_characters_archived_at",
                "(status = 'archived' AND archived_at IS NOT NULL) OR status <> 'archived'");
            t.HasCheckConstraint("ck_characters_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Role).HasMaxLength(20);
        b.Property(x => x.Sex).HasMaxLength(10);
        b.Property(x => x.FamilyName).HasMaxLength(20);
        b.Property(x => x.GivenName).HasMaxLength(30).IsRequired();
        b.Property(x => x.CourtesyName).HasMaxLength(30);
        b.Property(x => x.BirthDateLabel).HasMaxLength(30);
        b.Property(x => x.Appearance).HasMaxLength(3000).IsRequired();
        b.Property(x => x.Biography).HasMaxLength(2000).HasDefaultValue(string.Empty);
        b.Property(x => x.Personality).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.Strengths).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Weaknesses).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Likes).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Dislikes).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.PauseReason).HasMaxLength(500);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CharacterApplication>().WithMany()
            .HasForeignKey(x => x.SourceApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Portrait).WithMany()
            .HasForeignKey(x => x.PortraitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PlayerPortraitSubmission).WithMany()
            .HasForeignKey(x => x.PlayerPortraitSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Rank).WithMany()
            .HasForeignKey(x => x.RankId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Residence).WithMany()
            .HasForeignKey(x => x.ResidenceId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.SourceApplicationId).IsUnique();
        // Dead/archived history never blocks the same LINE account from rebuilding (§5.2).
        b.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("ux_characters_one_current_per_user")
            .HasFilter("status IN ('waiting_birth', 'active', 'paused', 'suspended')");
        b.HasIndex(x => new { x.Status, x.Role }).HasDatabaseName("ix_characters_status_role");
        b.HasIndex(x => new { x.FamilyName, x.GivenName }).HasDatabaseName("ix_characters_public_name");
    }
}

public class CharacterTitleAssignmentConfiguration : IEntityTypeConfiguration<CharacterTitleAssignment>
{
    public void Configure(EntityTypeBuilder<CharacterTitleAssignment> b)
    {
        b.ToTable("character_title_assignments", t =>
        {
            t.HasCheckConstraint("ck_cta_revoked_triple",
                "(revoked_at IS NULL AND revoked_by IS NULL AND revoke_reason IS NULL) OR " +
                "(revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND revoke_reason IS NOT NULL)");
            t.HasCheckConstraint("ck_cta_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.IsPrimary).HasDefaultValue(false);
        b.Property(x => x.GrantedAt).CreatedNow();
        b.Property(x => x.GrantReason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.RevokeReason).HasMaxLength(1000);
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithMany(c => c.Titles)
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TitleDefinition).WithMany()
            .HasForeignKey(x => x.TitleDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.RevokedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.CharacterId, x.TitleDefinitionId })
            .IsUnique()
            .HasDatabaseName("ux_character_title_assignments_active")
            .HasFilter("revoked_at IS NULL");
        b.HasIndex(x => x.CharacterId)
            .IsUnique()
            .HasDatabaseName("ux_character_title_assignments_one_primary")
            .HasFilter("revoked_at IS NULL AND is_primary = true");
    }
}

public class CharacterStatsConfiguration : IEntityTypeConfiguration<CharacterStats>
{
    public void Configure(EntityTypeBuilder<CharacterStats> b)
    {
        b.ToTable("character_stats", t =>
        {
            // 0–1000 scale, and deliberately no action-point column (§0.2, §6.12).
            t.HasCheckConstraint("ck_cs_vitality", "vitality BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_cs_appearance", "appearance BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_cs_strategy", "strategy BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_cs_luck", "luck BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_cs_prestige", "prestige >= 0");
            t.HasCheckConstraint("ck_cs_favor", "favor BETWEEN -1000 AND 1000");
            t.HasCheckConstraint("ck_cs_version", "version > 0");
        });

        b.HasKey(x => x.CharacterId);
        b.Property(x => x.CharacterId).ValueGeneratedNever();
        b.Property(x => x.Vitality).HasDefaultValue((short)0);
        b.Property(x => x.Appearance).HasDefaultValue((short)0);
        b.Property(x => x.Strategy).HasDefaultValue((short)0);
        b.Property(x => x.Luck).HasDefaultValue((short)0);
        b.Property(x => x.Prestige).HasDefaultValue(0L);
        b.Property(x => x.Favor).HasDefaultValue(0);
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithOne(c => c.Stats)
            .HasForeignKey<CharacterStats>(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CharacterStatusHistoryConfiguration : IEntityTypeConfiguration<CharacterStatusHistory>
{
    public void Configure(EntityTypeBuilder<CharacterStatusHistory> b)
    {
        b.ToTable("character_status_history");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.FromStatus).HasMaxLength(30);
        b.Property(x => x.ToStatus).HasMaxLength(30);
        b.Property(x => x.ReasonCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.ReasonText).HasMaxLength(1000);
        b.Property(x => x.RequestId).HasMaxLength(80);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.CharacterId, x.CreatedAt })
            .HasDatabaseName("ix_character_status_history_character");
    }
}

public class RankHistoryConfiguration : IEntityTypeConfiguration<RankHistory>
{
    public void Configure(EntityTypeBuilder<RankHistory> b)
    {
        b.ToTable("rank_history");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.ReasonCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.ReasonText).HasMaxLength(1000);
        b.Property(x => x.EffectiveAt).CreatedNow();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Rank>().WithMany().HasForeignKey(x => x.FromRankId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Rank>().WithMany().HasForeignKey(x => x.ToRankId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.CharacterId, x.EffectiveAt }).HasDatabaseName("ix_rank_history_character");
    }
}

public class CharacterResidenceHistoryConfiguration : IEntityTypeConfiguration<CharacterResidenceHistory>
{
    public void Configure(EntityTypeBuilder<CharacterResidenceHistory> b)
    {
        b.ToTable("character_residence_history", t =>
            t.HasCheckConstraint("ck_crh_order", "moved_out_at IS NULL OR moved_out_at >= moved_in_at"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Reason).HasMaxLength(500);

        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Residence>().WithMany()
            .HasForeignKey(x => x.ResidenceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.CharacterId)
            .IsUnique()
            .HasDatabaseName("ux_character_residence_current")
            .HasFilter("moved_out_at IS NULL");
    }
}
