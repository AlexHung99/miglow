using GongWei.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared column conventions so the 60 configurations stay short and consistent with
/// db/authoritative/schema_v1.0.sql.
/// </summary>
internal static class ConfigurationConventions
{
    /// <summary>
    /// Application-generated UUIDv7 primary key.
    ///
    /// <see cref="PropertyBuilder.ValueGeneratedNever"/> makes EF always send the id the
    /// application created, so inserts through the API stay time-ordered. The database
    /// default is still declared because rows also arrive from raw SQL — the two seed
    /// scripts insert without naming <c>id</c> and rely on it (schema_v1.1.sql).
    /// </summary>
    public static PropertyBuilder<Guid> ClientGeneratedKey(this PropertyBuilder<Guid> builder) =>
        builder.HasDefaultValueSql("gen_random_uuid()").ValueGeneratedNever();

    /// <summary>
    /// <c>version</c> and <c>updated_at</c> are owned by the <c>tr_*_touch</c> trigger in
    /// schema_v1.0.sql, which sets <c>version = OLD.version + 1</c> on every UPDATE.
    /// EF must therefore treat them as database-generated and read the new values back,
    /// otherwise its cached version drifts one behind after the first write.
    /// </summary>
    public static void DatabaseManagedVersion<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IVersioned
    {
        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(x => x.Version)
            .HasDefaultValue(1L)
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }

    /// <summary>A jsonb column that always holds an object, defaulting to <c>{}</c>.</summary>
    public static PropertyBuilder<string> JsonObject(this PropertyBuilder<string> builder) =>
        builder.HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");

    /// <summary>A jsonb column that always holds an array, defaulting to <c>[]</c>.</summary>
    public static PropertyBuilder<string> JsonArray(this PropertyBuilder<string> builder) =>
        builder.HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");

    /// <summary>A required jsonb column with no default — the caller must always supply it.</summary>
    public static PropertyBuilder<string> RequiredJson(this PropertyBuilder<string> builder) =>
        builder.HasColumnType("jsonb").IsRequired();

    public static PropertyBuilder<string?> NullableJson(this PropertyBuilder<string?> builder) =>
        builder.HasColumnType("jsonb");

    /// <summary>Lowercase hex digest stored as a fixed-width char column.</summary>
    public static PropertyBuilder<string> HexDigest(this PropertyBuilder<string> builder, int length) =>
        builder.HasMaxLength(length).IsFixedLength().IsRequired();

    public static PropertyBuilder<DateTimeOffset> CreatedNow(this PropertyBuilder<DateTimeOffset> builder) =>
        builder.HasDefaultValueSql("now()");
}
