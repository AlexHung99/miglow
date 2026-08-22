using GongWei.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Abstractions;

/// <summary>
/// Row-lock helpers. Every flow in spec §6 takes its locks in a fixed order, which is
/// what keeps concurrent settlements and birth draws from deadlocking:
/// reproduction_control(1) -> pregnancy -> wait pool entry -> character, and for
/// economy: market offer -> wallet -> inventory entry.
/// </summary>
public static class DbLocks
{
    /// <summary>
    /// Locks the reproduction singleton. Every flow that adds, miscarries or completes a
    /// pregnancy, or changes the waiting count, starts here (spec §6.2).
    /// </summary>
    public static Task LockReproductionControlAsync(this IGongWeiDb db, CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM game.reproduction_control WHERE singleton_id = 1 FOR UPDATE", ct);

    public static Task LockWorldStateAsync(this IGongWeiDb db, CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM game.world_state WHERE singleton_id = 1 FOR UPDATE", ct);

    /// <summary>
    /// Tables that may be row-locked. An allowlist rather than a raw parameter, so no
    /// future call site can turn a table name into an injection point.
    /// </summary>
    private static readonly HashSet<string> LockableTables = new(StringComparer.Ordinal)
    {
        "users",
        "characters",
        "character_applications",
        "player_portrait_submissions",
        "event_rooms",
        "event_posts",
        "market_offers",
        "wallets",
        "inventory_entries",
        "audience_requests",
        "pregnancies",
        "heir_wait_pool_entries",
        "approval_requests"
    };

    /// <summary>
    /// Takes FOR UPDATE on specific rows of a table. Ids are always ordered before the
    /// call so two concurrent transactions grab them in the same sequence.
    /// </summary>
    public static Task LockRowsAsync(
        this IGongWeiDb db,
        string table,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
    {
        if (!LockableTables.Contains(table))
        {
            throw new ArgumentOutOfRangeException(
                nameof(table), table, "This table is not in the row-lock allowlist.");
        }

        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        var ordered = ids.OrderBy(x => x).ToArray();

        // The table name is allowlisted above; the ids are a bound parameter.
#pragma warning disable EF1002
        return db.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM game.{table} WHERE id = ANY({{0}}) ORDER BY id FOR UPDATE",
            [ordered],
            ct);
#pragma warning restore EF1002
    }

    public static Task LockRowAsync(
        this IGongWeiDb db,
        string table,
        Guid id,
        CancellationToken ct = default) =>
        db.LockRowsAsync(table, [id], ct);

    /// <summary>Characters are always locked in UUID order (spec §6.6).</summary>
    public static Task LockCharactersAsync(
        this IGongWeiDb db,
        IReadOnlyCollection<Guid> characterIds,
        CancellationToken ct = default) =>
        db.LockRowsAsync("characters", characterIds, ct);
}
