using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GongWei.Application.Abstractions;
using GongWei.Domain.Operations;

namespace GongWei.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// CSPRNG-backed randomness. <see cref="RandomNumberGenerator.GetInt32(int)"/> is used
/// for the birth draw so the distribution is uniform and free of modulo bias (spec §6.4).
/// </summary>
public sealed class CryptoRandomProvider : IRandomProvider
{
    public int NextInt(int exclusiveUpperBound) => RandomNumberGenerator.GetInt32(exclusiveUpperBound);

    public byte[] NextBytes(int count) => RandomNumberGenerator.GetBytes(count);

    public string NextUrlSafeToken(int byteCount = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}

public sealed class SystemTextJsonSerializer : IJsonSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize(object? value) => JsonSerializer.Serialize(value, Options);

    public T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    public bool IsValidObject(string json) => IsValidKind(json, JsonValueKind.Object);

    public bool IsValidArray(string json) => IsValidKind(json, JsonValueKind.Array);

    private static bool IsValidKind(string json, JsonValueKind expected)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == expected;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Buffers audit rows onto the tracked DbContext so they commit with the business
/// change. An audit row that could be written without its change would be worse than
/// no audit at all (spec §11).
/// </summary>
public sealed class AuditWriter(
    Persistence.GongWeiDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IJsonSerializer json) : IAuditWriter
{
    public AuditLog Write(
        string action,
        string? targetType = null,
        Guid? targetId = null,
        object? before = null,
        object? after = null,
        string? reason = null)
    {
        var entry = new AuditLog
        {
            OccurredAt = clock.UtcNow,
            ActorUserId = currentUser.UserId,
            ActorRole = currentUser.AdminRoles.Count == 0
                ? null
                : string.Join(',', currentUser.AdminRoles.Select(Domain.Common.EnumNaming.ToDbValue)),
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            BeforeData = before is null ? null : json.Serialize(before),
            AfterData = after is null ? null : json.Serialize(after),
            Reason = reason,
            RequestId = currentUser.RequestId,
            IpAddress = ParseIp(currentUser.IpAddress),
            UserAgent = currentUser.UserAgent
        };

        db.AuditLogs.Add(entry);
        return entry;
    }

    /// <summary>The column is inet, so an unparseable value is dropped rather than failing the write.</summary>
    private static System.Net.IPAddress? ParseIp(string? value) =>
        System.Net.IPAddress.TryParse(value, out var address) ? address : null;
}

/// <summary>Transactional outbox writer — the message commits with the change (spec §10).</summary>
public sealed class OutboxWriter(
    Persistence.GongWeiDbContext db,
    IClock clock,
    IJsonSerializer json) : IOutboxWriter
{
    public void Enqueue(string topic, string aggregateType, Guid aggregateId, object payload)
    {
        var now = clock.UtcNow;

        db.OutboxMessages.Add(new OutboxMessage
        {
            Topic = topic,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Payload = json.Serialize(payload),
            OccurredAt = now,
            AvailableAt = now
        });
    }
}
