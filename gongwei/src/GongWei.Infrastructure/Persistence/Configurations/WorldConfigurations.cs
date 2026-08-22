using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Domain.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class WorldStateConfiguration : IEntityTypeConfiguration<WorldState>
{
    public void Configure(EntityTypeBuilder<WorldState> b)
    {
        b.ToTable("world_state", t =>
        {
            t.HasCheckConstraint("ck_world_state_singleton", "singleton_id = 1");
            t.HasCheckConstraint("ck_world_state_season",
                "season IN ('spring', 'summer', 'autumn', 'winter')");
            t.HasCheckConstraint("ck_world_state_calendar_mode", "calendar_mode = 'realtime_1to1'");
            t.HasCheckConstraint("ck_world_state_config", "jsonb_typeof(config) = 'object'");
            t.HasCheckConstraint("ck_world_state_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("singleton_id").ValueGeneratedNever().HasDefaultValue((short)1);
        b.Property(x => x.EraCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.DisplayYear).HasMaxLength(30).IsRequired();
        b.Property(x => x.Season).HasMaxLength(20);
        b.Property(x => x.DayLabel).HasMaxLength(30).IsRequired();
        b.Property(x => x.CalendarMode).HasMaxLength(20).HasDefaultValue(WorldState.RealtimeCalendarMode);
        b.Property(x => x.CalendarTimezone).HasMaxLength(50).HasDefaultValue("Asia/Taipei");
        b.Property(x => x.CalendarAnchorRealDate).HasDefaultValueSql("CURRENT_DATE");
        b.Property(x => x.CalendarAnchorGameDate).HasDefaultValueSql("CURRENT_DATE");
        b.Property(x => x.ReproductionOpen).HasDefaultValue(true);
        b.Property(x => x.MaintenanceMode).HasDefaultValue(false);
        b.Property(x => x.Config).JsonObject();
        b.DatabaseManagedVersion();
    }
}

public class GameSettingConfiguration : IEntityTypeConfiguration<GameSetting>
{
    public void Configure(EntityTypeBuilder<GameSetting> b)
    {
        b.ToTable("game_settings", t =>
        {
            t.HasCheckConstraint("ck_gs_risk_level", "risk_level IN ('normal', 'high')");
            t.HasCheckConstraint("ck_gs_validation_schema", "jsonb_typeof(validation_schema) = 'object'");
            t.HasCheckConstraint("ck_gs_key_len", "char_length(btrim(setting_key)) BETWEEN 3 AND 120");
            t.HasCheckConstraint("ck_gs_published_by", "published_at IS NULL OR published_by IS NOT NULL");
            t.HasCheckConstraint("ck_gs_version", "version > 0");
        });

        b.HasKey(x => x.SettingKey);
        b.Property(x => x.SettingKey).HasMaxLength(120);
        b.Property(x => x.Category).HasMaxLength(40).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000).HasDefaultValue(string.Empty);
        b.Property(x => x.PublishedValue).RequiredJson();
        b.Property(x => x.DraftValue).NullableJson();
        b.Property(x => x.ValidationSchema).RequiredJson();
        b.Property(x => x.RiskLevel).HasMaxLength(20).HasDefaultValue(SettingRiskLevel.Normal);
        b.Property(x => x.IsPublic).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

public class GameSettingRevisionConfiguration : IEntityTypeConfiguration<GameSettingRevision>
{
    public void Configure(EntityTypeBuilder<GameSettingRevision> b)
    {
        b.ToTable("game_setting_revisions", t =>
            t.HasCheckConstraint("ck_gsr_revision_no", "revision_no > 0"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.SettingKey).HasMaxLength(120).IsRequired();
        b.Property(x => x.PreviousValue).NullableJson();
        b.Property(x => x.PublishedValue).RequiredJson();
        b.Property(x => x.ChangeReason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ChangedAt).CreatedNow();

        b.HasOne(x => x.Setting).WithMany()
            .HasForeignKey(x => x.SettingKey)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Operations.ApprovalRequest>().WithMany()
            .HasForeignKey(x => x.ApprovalRequestId)
            .HasConstraintName("fk_game_setting_revisions_approval_request")
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.SettingKey, x.RevisionNo }).IsUnique();
    }
}

public class WorldLocationConfiguration : IEntityTypeConfiguration<WorldLocation>
{
    public void Configure(EntityTypeBuilder<WorldLocation> b)
    {
        b.ToTable("world_locations", t =>
        {
            t.HasCheckConstraint("ck_wl_map_x", "map_x BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_wl_map_y", "map_y BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_wl_access_rules", "jsonb_typeof(access_rules) = 'object'");
            t.HasCheckConstraint("ck_wl_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1500).HasDefaultValue(string.Empty);
        b.Property(x => x.MapX).HasColumnType("numeric(5,2)");
        b.Property(x => x.MapY).HasColumnType("numeric(5,2)");
        b.Property(x => x.AccessRules).JsonObject();
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class NpcConfiguration : IEntityTypeConfiguration<Npc>
{
    public void Configure(EntityTypeBuilder<Npc> b)
    {
        b.ToTable("npcs", t =>
        {
            t.HasCheckConstraint("ck_npc_sex", "sex IN ('female', 'male', 'unknown')");
            t.HasCheckConstraint("ck_npc_status",
                "status IN ('draft', 'review', 'published', 'archived')");
            t.HasCheckConstraint("ck_npc_public_profile", "jsonb_typeof(public_profile) = 'object'");
            t.HasCheckConstraint("ck_npc_display_name_len",
                "char_length(btrim(display_name)) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_npc_story_len", "char_length(story_markdown) <= 50000");
            // An NPC always has something to render.
            t.HasCheckConstraint("ck_npc_portrait_present",
                "portrait_asset_id IS NOT NULL OR portrait_url IS NOT NULL");
            t.HasCheckConstraint("ck_npc_published_pair",
                "(status = 'published' AND published_by IS NOT NULL AND published_at IS NOT NULL) " +
                "OR status <> 'published'");
            t.HasCheckConstraint("ck_npc_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Title).HasMaxLength(100).HasDefaultValue(string.Empty);
        b.Property(x => x.Sex).HasMaxLength(10);
        b.Property(x => x.Summary).HasMaxLength(1500).HasDefaultValue(string.Empty);
        b.Property(x => x.StoryMarkdown).HasDefaultValue(string.Empty);
        b.Property(x => x.PublicProfile).JsonObject();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(NpcStatus.Draft);
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne<MediaAsset>().WithMany()
            .HasForeignKey(x => x.PortraitAssetId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.PrimaryLocation).WithMany()
            .HasForeignKey(x => x.PrimaryLocationId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class NpcRevisionConfiguration : IEntityTypeConfiguration<NpcRevision>
{
    public void Configure(EntityTypeBuilder<NpcRevision> b)
    {
        b.ToTable("npc_revisions", t =>
        {
            t.HasCheckConstraint("ck_npcrev_revision_no", "revision_no > 0");
            t.HasCheckConstraint("ck_npcrev_snapshot", "jsonb_typeof(snapshot) = 'object'");
            t.HasCheckConstraint("ck_npcrev_change_kind",
                "change_kind IN ('create', 'edit', 'publish', 'archive', 'restore')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Snapshot).RequiredJson();
        b.Property(x => x.ChangeKind).HasMaxLength(20);
        b.Property(x => x.ChangeNote).HasMaxLength(1000);
        b.Property(x => x.ChangedAt).CreatedNow();

        b.HasOne(x => x.Npc).WithMany()
            .HasForeignKey(x => x.NpcId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.NpcId, x.RevisionNo }).IsUnique();
    }
}
