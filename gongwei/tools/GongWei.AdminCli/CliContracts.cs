using GongWei.Application.Abstractions;
using GongWei.Domain.Common;

namespace GongWei.AdminCli;

/// <summary>
/// Process exit codes fixed by implementation_bootstrap_v1.1 §9. Deployment scripts
/// branch on these, so the numbers are part of the contract.
/// </summary>
public static class ExitCode
{
    public const int Success = 0;
    public const int BadArguments = 2;
    public const int UserNotFound = 3;
    public const int DatabaseFailure = 4;
    public const int NotConfirmed = 5;

    /// <summary><c>verify-database</c> found the database usable but incomplete.</summary>
    public const int VerificationFailed = 6;
}

/// <summary>
/// Minimal <c>--name value</c> parser. Deliberately not System.CommandLine: the CLI has
/// three commands and adding a dependency to parse six flags is not a good trade.
/// </summary>
public sealed class CliArguments
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    private CliArguments(string command) => Command = command;

    public string Command { get; }

    public static CliArguments? Parse(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            return null;
        }

        var parsed = new CliArguments(args[0]);

        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            var name = args[i][2..];

            // A flag with no following value (or followed by another flag) is a boolean.
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            parsed._values[name] = hasValue ? args[++i] : null;
        }

        return parsed;
    }

    public string? Value(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public bool Has(string name) => _values.ContainsKey(name);
}

/// <summary>
/// <see cref="ICurrentUser"/> for a console session. There is no logged-in game account,
/// so audit rows carry a null actor and identify the operator through the request id —
/// bootstrap §9 requires the deploying identity to be recorded.
/// </summary>
public sealed class ConsoleOperator : ICurrentUser
{
    public ConsoleOperator() =>
        RequestId = $"admincli:{Environment.MachineName}\\{Environment.UserName}";

    public Guid? UserId => null;

    public bool IsAuthenticated => false;

    public IReadOnlySet<AdminRole> AdminRoles { get; } = new HashSet<AdminRole>();

    public string RequestId { get; }

    public string? IpAddress => null;

    public string UserAgent => "GongWei.AdminCli";

    public bool HasRole(AdminRole role) => false;

    public Guid RequireUserId() =>
        throw new InvalidOperationException("The CLI runs without a game account.");

    public void RequireRole(params AdminRole[] anyOf) =>
        throw new InvalidOperationException("The CLI runs without a game account.");
}

/// <summary>
/// Masks a LINE subject for display. Bootstrap §9.5 forbids printing it in full, but an
/// operator still has to recognise which account they are about to promote.
/// </summary>
public static class Masking
{
    public static string LineSub(string sub) =>
        sub.Length <= 10 ? new string('*', sub.Length) : $"{sub[..5]}…{sub[^4..]}";
}
