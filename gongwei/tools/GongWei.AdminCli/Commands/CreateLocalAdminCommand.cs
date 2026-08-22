using System.Runtime.InteropServices;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GongWei.AdminCli.Commands;

/// <summary>
/// Creates, or resets the password of, a local super admin for the control back office.
///
/// This account does not exist in backend_spec_v1.1, where admin identity also comes from
/// LINE Login. It is a second door into the most privileged role, so everything here is
/// arranged to keep that door narrow: the password is only ever typed at this console and
/// never passed as an argument, the account carries a synthetic LINE subject so it can
/// never obtain a player session, and both creation and reset are audited.
/// </summary>
public sealed class CreateLocalAdminCommand(
    GongWeiDbContext db,
    IPasswordHasher passwords,
    IClock clock,
    IAuditWriter audit)
{
    public async Task<int> RunAsync(CliArguments args, CancellationToken ct)
    {
        var username = args.Value("username")?.Trim();
        var displayName = args.Value("display-name")?.Trim();

        if (AdminPasswordPolicy.ValidateUsername(username) is { } usernameError)
        {
            Console.Error.WriteLine(usernameError);
            Console.Error.WriteLine(
                "Usage: create-local-admin --username <name> [--display-name '<shown in audit>']");
            return ExitCode.BadArguments;
        }

        // Never a --password flag. An argument lands in the shell history, in the process
        // list where any local account can read it, and in any script that wraps this.
        if (args.Has("password"))
        {
            Console.Error.WriteLine(
                "--password is not accepted. The password is typed at the prompt so it does not " +
                "reach the shell history or the process list.");
            return ExitCode.BadArguments;
        }

        var now = clock.UtcNow;
        var lineUserId = AdminPasswordPolicy.SyntheticLineUserId(username!);

        var existing = await db.AdminCredentials
            .AsTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Username.ToLower() == username!.ToLower(), ct);

        Console.WriteLine();

        if (existing is null)
        {
            Console.WriteLine("Creating a new local super admin.");
        }
        else
        {
            Console.WriteLine($"'{username}' already exists — this will reset its password.");
        }

        Console.WriteLine($"  Username : {username}");
        Console.WriteLine($"  Operator : {Environment.MachineName}\\{Environment.UserName}");
        Console.WriteLine();
        // ReadKey needs a real console. Say so plainly rather than letting it throw an
        // InvalidOperationException that looks like a bug.
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine(
                "This command needs an interactive console: the password is typed at the prompt, " +
                "never piped or passed as an argument. Run it directly in a terminal on the server.");
            return ExitCode.BadArguments;
        }

        Console.WriteLine(
            $"Password must be at least {AdminPasswordPolicy.MinPasswordLength} characters. " +
            "Input is hidden.");

        var password = ReadHidden("Password");

        if (AdminPasswordPolicy.ValidatePassword(password) is { } passwordError)
        {
            Console.Error.WriteLine(passwordError);
            return ExitCode.BadArguments;
        }

        var confirmation = ReadHidden("Confirm ");

        // Ordinal, not a culture-aware comparison: two strings that compare equal under a
        // culture's rules can still be different passwords.
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("The two entries do not match; nothing was changed.");
            return ExitCode.NotConfirmed;
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hash = passwords.Hash(password);

            if (existing is null)
            {
                var user = new User
                {
                    // Synthetic, and prefixed so it can never collide with a real LINE
                    // subject. This is also what stops the account getting a player
                    // session: the LINE callback is the only thing that mints one, and it
                    // will never see this value.
                    LineUserId = lineUserId,
                    DisplayName = displayName ?? username!,
                    Status = UserStatus.Active,
                    CreatedAt = now
                };

                db.Users.Add(user);
                await db.SaveChangesAsync(ct);

                db.AdminCredentials.Add(new AdminCredential
                {
                    UserId = user.Id,
                    Username = username!,
                    PasswordHash = hash,
                    MustChangePassword = false,
                    PasswordChangedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                db.AdminRoleAssignments.Add(new AdminRoleAssignment
                {
                    UserId = user.Id,
                    Role = AdminRole.SuperAdmin,
                    GrantedAt = now,
                    UpdatedAt = now
                });

                audit.Write(
                    action: "admin.local_account.create",
                    targetType: "user",
                    targetId: user.Id,
                    after: new { username, role = "super_admin", source = "admin_cli" },
                    reason: "local control back office account");
            }
            else
            {
                existing.PasswordHash = hash;
                existing.PasswordChangedAt = now;
                existing.FailedAttempts = 0;
                existing.LockedUntil = null;

                audit.Write(
                    action: "admin.local_account.reset_password",
                    targetType: "user",
                    targetId: existing.UserId,
                    after: new { username, source = "admin_cli" },
                    reason: "password reset from the server console");
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database transaction failed: {ex.Message}");
            return ExitCode.DatabaseFailure;
        }
        finally
        {
            password = null;
            confirmation = null;
            GC.Collect();
        }

        Console.WriteLine();
        Console.WriteLine(existing is null
            ? $"Created local super admin '{username}'."
            : $"Reset the password for '{username}'.");

        Console.WriteLine("This account signs in at the admin site only; it has no player session.");
        return ExitCode.Success;
    }

    /// <summary>
    /// Reads without echoing. Marshal rather than ConvertFrom-SecureString's managed
    /// equivalent so this works the same on every host the CLI might run under.
    /// </summary>
    private static string ReadHidden(string prompt)
    {
        Console.Write($"{prompt}: ");

        var secure = new System.Security.SecureString();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secure.Length > 0)
                {
                    secure.RemoveAt(secure.Length - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                secure.AppendChar(key.KeyChar);
            }
        }

        var pointer = Marshal.SecureStringToBSTR(secure);

        try
        {
            return Marshal.PtrToStringBSTR(pointer);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(pointer);
            secure.Dispose();
        }
    }
}
