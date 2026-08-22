using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GongWei.AdminCli.Commands;

/// <summary>
/// Bootstraps the very first super admin (implementation_bootstrap_v1.1 §9).
///
/// This is the one privilege escalation with no in-game approval behind it, which is why
/// it only runs on the server console, only against an account that has already completed
/// a LINE login, and only after the operator types the confirmation phrase in full.
/// </summary>
public sealed class GrantSuperAdminCommand(
    GongWeiDbContext db,
    IClock clock,
    IAuditWriter audit)
{
    /// <summary>Typed by the operator, or passed as <c>--confirm</c> from a controlled deploy script.</summary>
    public const string ConfirmationPhrase = "GRANT SUPER ADMIN";

    public async Task<int> RunAsync(CliArguments args, CancellationToken ct)
    {
        var lineUserId = args.Value("line-user-id");
        var reason = args.Value("reason");

        if (string.IsNullOrWhiteSpace(lineUserId) || string.IsNullOrWhiteSpace(reason))
        {
            Console.Error.WriteLine(
                "Usage: grant-super-admin --line-user-id <LINE_SUB> --reason '<why>' [--confirm '<phrase>']");
            return ExitCode.BadArguments;
        }

        var user = await db.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.LineUserId == lineUserId, ct);

        if (user is null)
        {
            // §7 puts the first LINE login before this command precisely so this cannot
            // be used to conjure an account that never authenticated.
            Console.Error.WriteLine(
                $"No user matches LINE subject {Masking.LineSub(lineUserId)}. " +
                "The account must sign in through LINE Login once before it can be promoted.");
            return ExitCode.UserNotFound;
        }

        var now = clock.UtcNow;

        var existing = await db.AdminRoleAssignments
            .AsTracking()
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.Role == AdminRole.SuperAdmin, ct);

        if (existing is not null && existing.IsActiveAt(now))
        {
            // Idempotent: re-running the deploy script must not fail or duplicate (§9.4).
            Console.WriteLine(
                $"Already a super admin: {user.DisplayName} ({user.Id}) — no change made.");
            return ExitCode.Success;
        }

        Console.WriteLine();
        Console.WriteLine("About to grant SUPER ADMIN — full access to every player record and the economy.");
        Console.WriteLine($"  User ID     : {user.Id}");
        Console.WriteLine($"  Display name: {user.DisplayName}");
        Console.WriteLine($"  LINE subject: {Masking.LineSub(lineUserId)}");
        Console.WriteLine($"  Reason      : {reason}");
        Console.WriteLine($"  Operator    : {Environment.MachineName}\\{Environment.UserName}");
        Console.WriteLine();

        if (!Confirmed(args))
        {
            Console.Error.WriteLine("Not confirmed — nothing was changed.");
            return ExitCode.NotConfirmed;
        }

        try
        {
            // One transaction for the role and its audit row: a grant that could commit
            // without its audit entry would defeat the point of auditing it.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            if (existing is null)
            {
                db.AdminRoleAssignments.Add(new AdminRoleAssignment
                {
                    UserId = user.Id,
                    Role = AdminRole.SuperAdmin,
                    GrantedBy = null,
                    GrantedAt = now,
                    ExpiresAt = null,
                    IsPublic = false,
                    SortOrder = 0,
                    UpdatedAt = now
                });
            }
            else
            {
                // The row exists but has lapsed — reinstate rather than insert a duplicate,
                // because (user_id, role) is the primary key.
                existing.ExpiresAt = null;
                existing.GrantedAt = now;
            }

            audit.Write(
                action: "admin.role.grant",
                targetType: "user",
                targetId: user.Id,
                after: new { role = "super_admin", grantedBy = "admin_cli" },
                reason: reason);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            // The message may name a table or a constraint; it must never carry the
            // connection string, so nothing but ex.Message is printed (§9.5).
            Console.Error.WriteLine($"Database transaction failed: {ex.Message}");
            return ExitCode.DatabaseFailure;
        }

        Console.WriteLine($"Granted super_admin to {user.DisplayName} ({user.Id}).");
        return ExitCode.Success;
    }

    /// <summary>
    /// Interactive by default. A non-interactive deploy passes <c>--confirm</c> with the
    /// exact phrase; anything shorter (a bare <c>--confirm</c>, or "yes") is refused.
    /// </summary>
    private static bool Confirmed(CliArguments args)
    {
        if (args.Has("confirm"))
        {
            return string.Equals(args.Value("confirm"), ConfirmationPhrase, StringComparison.Ordinal);
        }

        Console.Write($"Type '{ConfirmationPhrase}' to proceed: ");
        var typed = Console.ReadLine();

        return string.Equals(typed?.Trim(), ConfirmationPhrase, StringComparison.Ordinal);
    }
}
