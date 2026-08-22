using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Domain.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class AbilityLabelDefinitionConfiguration : IEntityTypeConfiguration<AbilityLabelDefinition>
{
    public void Configure(EntityTypeBuilder<AbilityLabelDefinition> b)
    {
        b.ToTable("ability_label_definitions", t =>
        {
            t.HasCheckConstraint("ck_ald_ability_code",
                "ability_code IN ('vitality', 'appearance', 'strategy', 'luck')");
            t.HasCheckConstraint("ck_ald_min", "min_value BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_ald_max", "max_value BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_ald_range", "min_value <= max_value");
            t.HasCheckConstraint("ck_ald_version", "version > 0");
        });

        // Composite key: one row per ability per range start.
        b.HasKey(x => new { x.AbilityCode, x.MinValue });
        b.Property(x => x.AbilityCode).HasMaxLength(20);
        b.Property(x => x.DisplayLabel).HasMaxLength(30).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500).HasDefaultValue(string.Empty);
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => new { x.AbilityCode, x.DisplayLabel }).IsUnique();
    }
}

public class CharacterProgressConfiguration : IEntityTypeConfiguration<CharacterProgress>
{
    public void Configure(EntityTypeBuilder<CharacterProgress> b)
    {
        b.ToTable("character_progress", t =>
        {
            t.HasCheckConstraint("ck_cp_settled_events", "settled_event_count >= 0");
            t.HasCheckConstraint("ck_cp_approved_posts", "approved_event_post_count >= 0");
            t.HasCheckConstraint("ck_cp_approved_external", "approved_external_play_count >= 0");
            t.HasCheckConstraint("ck_cp_self_play_words", "self_play_word_count >= 0");
            t.HasCheckConstraint("ck_cp_weekly_messages", "weekly_message_count >= 0");
            t.HasCheckConstraint("ck_cp_version", "version > 0");
        });

        b.HasKey(x => x.CharacterId);
        b.Property(x => x.CharacterId).ValueGeneratedNever();
        b.Property(x => x.SettledEventCount).HasDefaultValue(0L);
        b.Property(x => x.ApprovedEventPostCount).HasDefaultValue(0L);
        b.Property(x => x.ApprovedExternalPlayCount).HasDefaultValue(0L);
        b.Property(x => x.SelfPlayWordCount).HasDefaultValue(0L);
        b.Property(x => x.WeeklyMessageCount).HasDefaultValue(0);
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithOne()
            .HasForeignKey<CharacterProgress>(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CharacterChronicleEntryConfiguration : IEntityTypeConfiguration<CharacterChronicleEntry>
{
    public void Configure(EntityTypeBuilder<CharacterChronicleEntry> b)
    {
        b.ToTable("character_chronicle_entries", t =>
        {
            t.HasCheckConstraint("ck_cce_entry_type",
                "entry_type IN ('event', 'economy', 'inventory', 'rank', 'status', " +
                "'reproduction', 'intrigue', 'admin', 'system')");
            t.HasCheckConstraint("ck_cce_visibility",
                "visibility IN ('public', 'owner_only', 'admin_only')");
            t.HasCheckConstraint("ck_cce_stat_changes", "jsonb_typeof(stat_changes) = 'array'");
            t.HasCheckConstraint("ck_cce_resource_changes", "jsonb_typeof(resource_changes) = 'array'");
            t.HasCheckConstraint("ck_cce_metadata", "jsonb_typeof(metadata) = 'object'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.EntryType).HasMaxLength(30);
        b.Property(x => x.Visibility).HasMaxLength(20).HasDefaultValue(ChronicleVisibility.Public);
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(3000).HasDefaultValue(string.Empty);
        b.Property(x => x.SourceType).HasMaxLength(60).IsRequired();
        b.Property(x => x.StatChanges).JsonArray();
        b.Property(x => x.ResourceChanges).JsonArray();
        b.Property(x => x.RequestId).HasMaxLength(80);
        b.Property(x => x.Metadata).JsonObject();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorldLocation>().WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        // The player's own timeline, newest first.
        b.HasIndex(x => new { x.CharacterId, x.HappenedAt, x.Id })
            .HasDatabaseName("ix_character_chronicle_character");
        b.HasIndex(x => new { x.SourceType, x.SourceId })
            .HasDatabaseName("ix_character_chronicle_source");
    }
}

public class LineLoginAttemptConfiguration : IEntityTypeConfiguration<LineLoginAttempt>
{
    public void Configure(EntityTypeBuilder<LineLoginAttempt> b)
    {
        b.ToTable("line_login_attempts", t =>
        {
            t.HasCheckConstraint("ck_lla_expiry", "expires_at > created_at");
            // Closes the open-redirect hole at the database level: the return URL can
            // only ever point at the player front end.
            t.HasCheckConstraint("ck_lla_return_url",
                "return_url LIKE 'https://miglow.vip/gongwei/%' OR " +
                "return_url = 'https://miglow.vip/gongwei/'");
            t.HasCheckConstraint("ck_lla_consumed", "consumed_at IS NULL OR consumed_at >= created_at");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.StateHash).IsRequired();
        b.Property(x => x.NonceHash).IsRequired();
        b.Property(x => x.ProtectedPayload).IsRequired();
        b.Property(x => x.ReturnUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.FailureCode).HasMaxLength(80);
        b.Property(x => x.CreatedAt).CreatedNow();

        // A replayed state must collide here rather than start a second session.
        b.HasIndex(x => x.StateHash).IsUnique();
    }
}
