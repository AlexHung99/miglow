namespace GongWei.Domain.Common;

/// <summary>
/// A mutable aggregate carrying an optimistic-concurrency token.
///
/// In v1.0 the database owns both fields: the <c>tr_*_touch</c> trigger in
/// schema_v1.0.sql sets <c>updated_at = now()</c> and <c>version = OLD.version + 1</c>
/// on every UPDATE. The application therefore never assigns them — EF maps them as
/// database-generated and reads the new value back after the write.
/// </summary>
public interface IVersioned
{
    long Version { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Anything addressed by a uuid primary key.</summary>
public interface IHasId
{
    Guid Id { get; }
}

public static class VersionedExtensions
{
    /// <summary>
    /// Guards a write against a stale <c>If-Match</c> before the update is attempted, so
    /// the caller gets VERSION_CONFLICT rather than a silent overwrite. EF's concurrency
    /// token is the second line of defence for races that slip past this check.
    /// </summary>
    public static void EnsureVersion(this IVersioned entity, long? expectedVersion)
    {
        if (expectedVersion is not null && expectedVersion.Value != entity.Version)
        {
            throw DomainException.VersionConflict(entity.Version);
        }
    }
}
