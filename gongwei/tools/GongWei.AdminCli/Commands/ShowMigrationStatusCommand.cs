using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GongWei.AdminCli.Commands;

/// <summary>
/// Prints applied and pending migrations. Run before and after a deploy so an operator
/// can tell "the new binaries are live" apart from "the new schema is live".
/// </summary>
public sealed class ShowMigrationStatusCommand(GongWeiDbContext db)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        List<string> applied;
        List<string> pending;

        try
        {
            applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
            pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        }
        catch (NpgsqlException ex)
        {
            Console.Error.WriteLine($"Cannot reach the database: {ex.Message}");
            return ExitCode.DatabaseFailure;
        }

        Console.WriteLine();
        Console.WriteLine($"Applied migrations ({applied.Count}):");

        foreach (var migration in applied)
        {
            Console.WriteLine($"  ✓ {migration}");
        }

        if (applied.Count == 0)
        {
            Console.WriteLine("  (none — the database has never been migrated)");
        }

        Console.WriteLine();
        Console.WriteLine($"Pending migrations ({pending.Count}):");

        foreach (var migration in pending)
        {
            Console.WriteLine($"  · {migration}");
        }

        if (pending.Count == 0)
        {
            Console.WriteLine("  (none — the schema is up to date)");
        }

        Console.WriteLine();

        // Non-zero on pending work so a deploy script can gate on it.
        return pending.Count == 0 ? ExitCode.Success : ExitCode.VerificationFailed;
    }
}
