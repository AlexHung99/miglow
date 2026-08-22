using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_status", "status IN ('active', 'suspended', 'deleted')");
            t.HasCheckConstraint("ck_users_line_user_id_len",
                "char_length(btrim(line_user_id)) BETWEEN 1 AND 255");
            t.HasCheckConstraint("ck_users_display_name_len",
                "char_length(btrim(display_name)) BETWEEN 1 AND 80");
            t.HasCheckConstraint("ck_users_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.LineUserId).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        b.Property(x => x.Locale).HasMaxLength(16).HasDefaultValue("zh-TW");
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(UserStatus.Active);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => x.LineUserId).IsUnique();
    }
}

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("user_sessions", t =>
        {
            t.HasCheckConstraint("ck_user_sessions_expiry_order",
                "idle_expires_at <= absolute_expires_at");
            t.HasCheckConstraint("ck_user_sessions_absolute_after_created",
                "absolute_expires_at > created_at");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.TokenHash).IsRequired();
        b.Property(x => x.CsrfSecretHash).IsRequired();
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.RevokeReason).HasMaxLength(200);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.Property(x => x.LastSeenAt).CreatedNow();

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.AbsoluteExpiresAt })
            .HasDatabaseName("ix_user_sessions_active_user")
            .HasFilter("revoked_at IS NULL");
    }
}

public class AdminRoleAssignmentConfiguration : IEntityTypeConfiguration<AdminRoleAssignment>
{
    public void Configure(EntityTypeBuilder<AdminRoleAssignment> b)
    {
        b.ToTable("admin_role_assignments", t =>
        {
            t.HasCheckConstraint("ck_admin_role_assignments_role",
                "role IN ('super_admin', 'character_reviewer', 'game_master', 'economy_manager', " +
                "'moderator', 'auditor', 'content_editor', 'character_manager', 'system_config_manager')");
            t.HasCheckConstraint("ck_admin_role_assignments_expiry",
                "expires_at IS NULL OR expires_at > granted_at");
            // Nothing reaches the public 執事 page without a display name to show.
            t.HasCheckConstraint("ck_admin_role_assignments_public",
                "is_public = false OR public_display_name IS NOT NULL");
            t.HasCheckConstraint("ck_admin_role_assignments_version", "version > 0");
        });

        b.HasKey(x => new { x.UserId, x.Role });
        b.Property(x => x.Role).HasMaxLength(40);
        b.Property(x => x.GrantedAt).CreatedNow();
        b.Property(x => x.PublicDisplayName).HasMaxLength(80);
        b.Property(x => x.PublicTitle).HasMaxLength(80);
        b.Property(x => x.PublicDuty).HasMaxLength(500);
        b.Property(x => x.IsPublic).HasDefaultValue(false);
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.DatabaseManagedVersion();

        b.HasOne(x => x.User).WithMany(u => u.AdminRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.GrantedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PresetPortraitConfiguration : IEntityTypeConfiguration<PresetPortrait>
{
    public void Configure(EntityTypeBuilder<PresetPortrait> b)
    {
        b.ToTable("preset_portraits", t =>
        {
            t.HasCheckConstraint("ck_preset_portraits_role",
                "role IN ('consort', 'prince', 'princess')");
            t.HasCheckConstraint("ck_preset_portraits_metadata", "jsonb_typeof(metadata) = 'object'");
            t.HasCheckConstraint("ck_preset_portraits_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.Role).HasMaxLength(20);
        b.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        b.Property(x => x.AssetUrl).IsRequired();
        b.Property(x => x.SortOrder).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.Metadata).JsonObject();
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.ToTable("media_assets", t =>
        {
            t.HasCheckConstraint("ck_media_assets_original_mime",
                "original_mime_type IN ('image/jpeg', 'image/png', 'image/webp')");
            t.HasCheckConstraint("ck_media_assets_stored_mime",
                "stored_mime_type IS NULL OR stored_mime_type IN ('image/webp', 'image/jpeg')");
            t.HasCheckConstraint("ck_media_assets_byte_size", "byte_size BETWEEN 1 AND 8388608");
            t.HasCheckConstraint("ck_media_assets_width", "width >= 600");
            t.HasCheckConstraint("ck_media_assets_height", "height >= 800");
            t.HasCheckConstraint("ck_media_assets_status",
                "status IN ('uploaded', 'processing', 'ready', 'quarantined', 'deleted')");
            t.HasCheckConstraint("ck_media_assets_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_media_assets_storage_key_len",
                "char_length(btrim(storage_key)) BETWEEN 1 AND 1024");
            t.HasCheckConstraint("ck_media_assets_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.StorageKey).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.OriginalMimeType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StoredMimeType).HasMaxLength(30);
        b.Property(x => x.Sha256).HexDigest(64);
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(MediaAssetStatus.Uploaded);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Owner).WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedAt })
            .HasDatabaseName("ix_media_assets_owner_created")
            .HasFilter("status <> 'deleted'");
    }
}

public class PlayerPortraitSubmissionConfiguration : IEntityTypeConfiguration<PlayerPortraitSubmission>
{
    public void Configure(EntityTypeBuilder<PlayerPortraitSubmission> b)
    {
        b.ToTable("player_portrait_submissions", t =>
        {
            t.HasCheckConstraint("ck_pps_role", "role IN ('consort', 'prince', 'princess')");
            t.HasCheckConstraint("ck_pps_crop_x", "crop_x BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_pps_crop_y", "crop_y BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_pps_crop_width", "crop_width > 0 AND crop_width <= 1");
            t.HasCheckConstraint("ck_pps_crop_height", "crop_height > 0 AND crop_height <= 1");
            t.HasCheckConstraint("ck_pps_status",
                "status IN ('pending', 'approved', 'rejected', 'withdrawn')");
            t.HasCheckConstraint("ck_pps_crop_x_bounds", "crop_x + crop_width <= 1.00001");
            t.HasCheckConstraint("ck_pps_crop_y_bounds", "crop_y + crop_height <= 1.00001");
            t.HasCheckConstraint("ck_pps_reviewed_pair",
                "(status IN ('approved', 'rejected') AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL) " +
                "OR status IN ('pending', 'withdrawn')");
            t.HasCheckConstraint("ck_pps_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Role).HasMaxLength(20);
        b.Property(x => x.CropX).HasColumnType("numeric(6,5)").HasDefaultValue(0m);
        b.Property(x => x.CropY).HasColumnType("numeric(6,5)").HasDefaultValue(0m);
        b.Property(x => x.CropWidth).HasColumnType("numeric(6,5)").HasDefaultValue(1m);
        b.Property(x => x.CropHeight).HasColumnType("numeric(6,5)").HasDefaultValue(1m);
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(PortraitSubmissionStatus.Pending);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MediaAsset).WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.MediaAssetId).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_player_portrait_submissions_review_queue")
            .HasFilter("status = 'pending'");
    }
}

/// <summary>
/// Table: admin_credentials. Not present in schema_v1.1.sql — added by the
/// LocalAdminCredentials migration, which is the only table in this database that has no
/// counterpart in the authoritative file.
/// </summary>
public class AdminCredentialConfiguration : IEntityTypeConfiguration<AdminCredential>
{
    public void Configure(EntityTypeBuilder<AdminCredential> b)
    {
        b.ToTable("admin_credentials", t =>
        {
            t.HasCheckConstraint("ck_admin_credentials_username",
                "char_length(btrim(username)) BETWEEN 3 AND 64");
            t.HasCheckConstraint("ck_admin_credentials_failed", "failed_attempts >= 0");
            t.HasCheckConstraint("ck_admin_credentials_version", "version > 0");
        });

        // One credential per user, enforced by making the user the key rather than by a
        // unique index on a separate id.
        b.HasKey(x => x.UserId);

        b.Property(x => x.UserId).ValueGeneratedNever();
        b.Property(x => x.Username).HasMaxLength(64).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.MustChangePassword).HasDefaultValue(false);
        b.Property(x => x.FailedAttempts).HasDefaultValue(0);
        b.Property(x => x.PasswordChangedAt).CreatedNow();
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The unique index is created by the migration as lower(username), which EF has no
        // way to express here. Declaring a plain unique index as well would produce a
        // second, weaker constraint and a spurious model difference on every scaffold.
        b.HasIndex(x => x.Username).HasDatabaseName("ix_admin_credentials_username");
    }
}
