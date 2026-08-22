using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GongWei.AdminCli.Commands;

/// <summary>
/// Step 8 of the initialisation order (bootstrap §7): proves the database is not just
/// migrated but actually seeded and hardened.
///
/// Every check runs even after one fails, so a single run tells the operator everything
/// that is wrong rather than only the first thing.
/// </summary>
public sealed class VerifyDatabaseCommand(GongWeiDbContext db, IClock clock)
{
    // Counts fixed by bootstrap §9 and the two seed scripts.
    private const int ExpectedTables = 60;
    private const int ExpectedAbilityLabels = 28;
    private const int ExpectedLocations = 8;
    private const int ExpectedPublishedNpcs = 8;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var results = new List<CheckResult>();

        try
        {
            results.Add(await CheckMigrationsAsync(ct));
            results.Add(await CheckTableCountAsync(ct));
            results.Add(await CheckTriggersAsync(ct));
            results.Add(await CheckUuidDefaultsAsync(ct));
            results.Add(await CheckSuperAdminAsync(ct));
            results.Add(await CheckRulesAsync(ct));
            results.Add(await CheckAbilityLabelsAsync(ct));
            results.Add(await CheckLocationsAsync(ct));
            results.Add(await CheckNpcsAsync(ct));
        }
        catch (NpgsqlException ex)
        {
            Console.Error.WriteLine($"Cannot reach the database: {ex.Message}");
            return ExitCode.DatabaseFailure;
        }

        Console.WriteLine();
        Console.WriteLine("宮闈浮生 database verification");
        Console.WriteLine(new string('-', 64));

        foreach (var result in results)
        {
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")}  {result.Name,-26} {result.Detail}");
        }

        Console.WriteLine(new string('-', 64));

        var failures = results.Count(r => !r.Passed);

        if (failures == 0)
        {
            Console.WriteLine($"All {results.Count} checks passed.");
            return ExitCode.Success;
        }

        Console.Error.WriteLine($"{failures} of {results.Count} checks failed.");
        return ExitCode.VerificationFailed;
    }

    private async Task<CheckResult> CheckMigrationsAsync(CancellationToken ct)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

        return new CheckResult(
            "migrations",
            pending.Count == 0 && applied.Count > 0,
            pending.Count == 0
                ? $"{applied.Count} applied, latest {applied.LastOrDefault() ?? "(none)"}"
                : $"{pending.Count} pending: {string.Join(", ", pending)}");
    }

    private async Task<CheckResult> CheckTableCountAsync(CancellationToken ct)
    {
        var count = await ScalarAsync(
            """
            SELECT count(*)::int FROM information_schema.tables
            WHERE table_schema = 'game' AND table_type = 'BASE TABLE'
              AND table_name <> '__ef_migrations_history'
            """,
            ct);

        return new CheckResult("tables", count == ExpectedTables, $"{count} of {ExpectedTables}");
    }

    private async Task<CheckResult> CheckTriggersAsync(CancellationToken ct)
    {
        // The touch/append-only triggers are what make version and immutability real;
        // an EF-only migration would silently produce none of them.
        var count = await ScalarAsync(
            """
            SELECT count(*)::int FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'game' AND NOT t.tgisinternal
            """,
            ct);

        return new CheckResult("triggers", count > 0, $"{count} present");
    }

    private async Task<CheckResult> CheckUuidDefaultsAsync(CancellationToken ct)
    {
        // Guards the bug found during bring-up: EF generated the columns but dropped every
        // gen_random_uuid() default, which would have broken both seed scripts.
        var count = await ScalarAsync(
            """
            SELECT count(*)::int FROM information_schema.columns
            WHERE table_schema = 'game' AND column_default LIKE 'gen_random_uuid%'
            """,
            ct);

        return new CheckResult("uuid defaults", count >= 50, $"{count} columns default to gen_random_uuid()");
    }

    private async Task<CheckResult> CheckSuperAdminAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        var count = await db.AdminRoleAssignments
            .CountAsync(a => a.Role == AdminRole.SuperAdmin && (a.ExpiresAt == null || a.ExpiresAt > now), ct);

        return new CheckResult(
            "active super admin",
            count > 0,
            count > 0 ? $"{count} active" : "none — run grant-super-admin");
    }

    private async Task<CheckResult> CheckRulesAsync(CancellationToken ct)
    {
        var settings = await db.GameSettings.CountAsync(ct);
        var ranks = await db.Ranks.CountAsync(ct);

        return new CheckResult(
            "rules seed",
            settings > 0 && ranks > 0,
            $"{settings} settings, {ranks} ranks");
    }

    private async Task<CheckResult> CheckAbilityLabelsAsync(CancellationToken ct)
    {
        var count = await db.AbilityLabelDefinitions.CountAsync(ct);

        return new CheckResult(
            "ability labels",
            count == ExpectedAbilityLabels,
            $"{count} of {ExpectedAbilityLabels}");
    }

    private async Task<CheckResult> CheckLocationsAsync(CancellationToken ct)
    {
        var count = await db.WorldLocations.CountAsync(ct);

        return new CheckResult("world locations", count == ExpectedLocations, $"{count} of {ExpectedLocations}");
    }

    private async Task<CheckResult> CheckNpcsAsync(CancellationToken ct)
    {
        var count = await db.Npcs.CountAsync(n => n.Status == NpcStatus.Published, ct);

        return new CheckResult(
            "published NPCs",
            count == ExpectedPublishedNpcs,
            $"{count} of {ExpectedPublishedNpcs}");
    }

    private async Task<int> ScalarAsync(string sql, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await db.Database.OpenConnectionAsync(ct);

        try
        {
            var value = await command.ExecuteScalarAsync(ct);
            return value is null or DBNull ? 0 : Convert.ToInt32(value);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private sealed record CheckResult(string Name, bool Passed, string Detail);
}
