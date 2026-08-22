using GongWei.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GongWei.Infrastructure.Persistence.Converters;

/// <summary>
/// Persists a C# enum as its snake_cased member name, matching the CHECK constraint
/// lists in db/schema_v0.8.sql. Reading back an unknown value throws rather than
/// silently landing on the default member.
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(
            value => EnumNaming.ToDbValue(value),
            dbValue => EnumNaming.FromDbValue<TEnum>(dbValue))
    {
    }
}
