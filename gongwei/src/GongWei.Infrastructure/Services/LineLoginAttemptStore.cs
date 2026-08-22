using System.Net;
using GongWei.Application.Abstractions;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace GongWei.Infrastructure.Services;

/// <summary>
/// Durable storage for LINE login attempts (line_login_v1.1 §7).
///
/// <see cref="ConsumeAsync"/> is the interesting one: it is a single conditional UPDATE …
/// RETURNING, so two callbacks racing on the same state cannot both win. A read followed
/// by a write would let both pass the "not consumed yet" test before either wrote.
/// </summary>
public sealed class LineLoginAttemptStore(GongWeiDbContext db) : ILineLoginAttemptStore
{
    public async Task CreateAsync(
        byte[] stateHash,
        byte[] nonceHash,
        byte[] protectedPayload,
        string returnUrl,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        db.LineLoginAttempts.Add(new Domain.Identity.LineLoginAttempt
        {
            StateHash = stateHash,
            NonceHash = nonceHash,
            ProtectedPayload = protectedPayload,
            ReturnUrl = returnUrl,
            IpAddress = ParseIp(ipAddress),
            UserAgent = userAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<LoginAttemptConsumeResult> ConsumeAsync(
        byte[] stateHash,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;

        if (opened)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();

            // Enlist in the ambient EF transaction if one is open, so a caller that has
            // already begun a transaction sees a consistent view.
            if (db.Database.CurrentTransaction?.GetDbTransaction() is NpgsqlTransaction ambient)
            {
                command.Transaction = ambient;
            }

            command.CommandText =
                """
                UPDATE game.line_login_attempts
                   SET consumed_at = @now
                 WHERE state_hash = @state_hash
                   AND consumed_at IS NULL
                   AND expires_at > @now
                RETURNING id, protected_payload, return_url
                """;

            command.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now });
            command.Parameters.Add(new NpgsqlParameter("state_hash", NpgsqlDbType.Bytea) { Value = stateHash });

            await using var reader = await command.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
            {
                var attempt = new RedeemedLoginAttempt(
                    reader.GetGuid(0),
                    (byte[])reader.GetValue(1),
                    reader.GetString(2));

                return new LoginAttemptConsumeResult(LoginAttemptStatus.Consumed, attempt);
            }
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }

        // Nothing was updated. Work out why — purely so the player gets an accurate
        // message; the security decision was already made above.
        return new LoginAttemptConsumeResult(await ClassifyFailureAsync(stateHash, now, ct), null);
    }

    public async Task RecordFailureAsync(
        Guid attemptId,
        string failureCode,
        CancellationToken ct = default)
    {
        // ExecuteUpdate, not a tracked write: the caller may be about to roll its
        // transaction back, and the failure record should survive that.
        await db.LineLoginAttempts
            .Where(a => a.Id == attemptId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.FailureCode, failureCode), ct);
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset expiredBefore, CancellationToken ct = default) =>
        db.LineLoginAttempts
            .Where(a => a.ExpiresAt < expiredBefore)
            .ExecuteDeleteAsync(ct);

    private async Task<LoginAttemptStatus> ClassifyFailureAsync(
        byte[] stateHash,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var found = await db.LineLoginAttempts
            .AsNoTracking()
            .Where(a => a.StateHash == stateHash)
            .Select(a => new { a.ConsumedAt, a.ExpiresAt })
            .FirstOrDefaultAsync(ct);

        if (found is null)
        {
            return LoginAttemptStatus.NotFound;
        }

        // Replay outranks expiry: a state that was already redeemed is the more serious
        // of the two, and it is what an operator needs to see in the metrics.
        return found.ConsumedAt is not null
            ? LoginAttemptStatus.AlreadyConsumed
            : LoginAttemptStatus.Expired;
    }

    private static IPAddress? ParseIp(string? value) =>
        IPAddress.TryParse(value, out var address) ? address : null;
}

/// <summary>
/// Seals the nonce and PKCE verifier with ASP.NET Core Data Protection.
///
/// The key ring must be shared and persisted, otherwise an app-pool recycle between
/// start and callback would invalidate every in-flight login (§8.4).
/// </summary>
public sealed class DataProtectionPayloadProtector(IDataProtectionProvider provider) : IPayloadProtector
{
    public byte[] Protect(string purpose, string plaintext) =>
        provider.CreateProtector(purpose).Protect(System.Text.Encoding.UTF8.GetBytes(plaintext));

    public string? Unprotect(string purpose, byte[] sealedPayload)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(
                provider.CreateProtector(purpose).Unprotect(sealedPayload));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // A rotated-out key or a tampered payload. The caller turns this into a
            // generic error; the exception itself must not surface, because its message
            // can name key ring internals.
            return null;
        }
    }
}
