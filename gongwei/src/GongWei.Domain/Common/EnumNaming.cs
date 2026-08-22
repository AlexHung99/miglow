using System.Collections.Concurrent;
using System.Text;

namespace GongWei.Domain.Common;

/// <summary>
/// Single source of truth for how a C# enum member is spelled in PostgreSQL and
/// in JSON. <c>WaitingBirth</c> becomes <c>waiting_birth</c>, matching the CHECK
/// constraints in db/schema_v0.8.sql.
/// </summary>
public static class EnumNaming
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, string>> ToDb = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<string, string>> FromDb = new();

    public static string ToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        var sb = new StringBuilder(pascalCase.Length + 4);
        for (var i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static string ToDbValue<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var map = ToDb.GetOrAdd(typeof(TEnum), static t =>
            Enum.GetNames(t).ToDictionary(n => n, ToSnakeCase, StringComparer.Ordinal));

        return map[value.ToString()];
    }

    public static TEnum FromDbValue<TEnum>(string dbValue) where TEnum : struct, Enum
    {
        var map = FromDb.GetOrAdd(typeof(TEnum), static t =>
            Enum.GetNames(t).ToDictionary(ToSnakeCase, n => n, StringComparer.Ordinal));

        if (!map.TryGetValue(dbValue, out var name))
        {
            throw new InvalidOperationException(
                $"'{dbValue}' is not a valid {typeof(TEnum).Name}. The database and the enum have drifted apart.");
        }

        return Enum.Parse<TEnum>(name);
    }

    /// <summary>All persisted spellings of an enum — used to assert against the DB CHECK lists in tests.</summary>
    public static IReadOnlyList<string> AllDbValues<TEnum>() where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>().Select(ToSnakeCase).ToList();
}
