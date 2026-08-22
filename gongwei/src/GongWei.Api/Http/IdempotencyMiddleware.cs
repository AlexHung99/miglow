using System.Security.Cryptography;
using System.Text;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Operations;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Http;

/// <summary>
/// Marks a route as requiring <c>Idempotency-Key</c> (spec §8.1). Applied as endpoint
/// metadata so the middleware can find it without reflecting over controllers.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireIdempotencyAttribute : Attribute;

/// <summary>
/// Implements the replay algorithm from spec §8.2:
/// same key + same body + completed → replay the stored response;
/// same key + different body → 409 IDEMPOTENCY_KEY_REUSED;
/// still processing → 409 REQUEST_IN_PROGRESS.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";

    private static readonly TimeSpan RecordLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(2);

    public async Task InvokeAsync(
        HttpContext context,
        GongWeiDbContext db,
        IClock clock,
        ICurrentUser currentUser)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<RequireIdempotencyAttribute>() is null)
        {
            await next(context);
            return;
        }

        if (currentUser.UserId is not { } userId)
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            || headerValues.ToString() is not { Length: > 0 } key)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                ErrorCodes.IdempotencyKeyRequired,
                $"This endpoint requires an {HeaderName} header.");
            return;
        }

        var requestHash = await ComputeRequestHashAsync(context);
        var normalizedPath = context.Request.Path.Value?.TrimEnd('/').ToLowerInvariant() ?? "/";
        var method = context.Request.Method.ToUpperInvariant();
        var now = clock.UtcNow;

        var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(
            r => r.UserId == userId
                 && r.HttpMethod == method
                 && r.RequestPath == normalizedPath
                 && r.IdempotencyKey == key,
            context.RequestAborted);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    ErrorCodes.IdempotencyKeyReused,
                    "That Idempotency-Key was already used with a different request body.");
                return;
            }

            if (existing.Status == IdempotencyStatus.Completed)
            {
                logger.LogInformation("Replaying idempotent response for {Method} {Path}", method, normalizedPath);

                context.Response.StatusCode = existing.ResponseStatus ?? StatusCodes.Status200OK;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["Idempotency-Replayed"] = "true";

                if (existing.ResponseBody is { } body)
                {
                    await context.Response.WriteAsync(body, context.RequestAborted);
                }

                return;
            }

            if (!existing.IsStaleProcessing(now, ProcessingTimeout))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    ErrorCodes.RequestInProgress,
                    "An identical request is still being processed. Retry shortly.");
                return;
            }

            // A processing record older than the timeout is a crashed attempt; take it over.
            existing.Status = IdempotencyStatus.Processing;
            existing.CreatedAt = now;
            existing.ExpiresAt = now.Add(RecordLifetime);
            await db.SaveChangesAsync(context.RequestAborted);
        }
        else
        {
            var record = new IdempotencyRecord
            {
                UserId = userId,
                HttpMethod = method,
                RequestPath = normalizedPath,
                IdempotencyKey = key,
                RequestHash = requestHash,
                Status = IdempotencyStatus.Processing,
                CreatedAt = now,
                ExpiresAt = now.Add(RecordLifetime)
            };

            db.IdempotencyRecords.Add(record);

            try
            {
                await db.SaveChangesAsync(context.RequestAborted);
            }
            catch (DbUpdateException)
            {
                // Another request inserted the same key first — that one owns it.
                db.ChangeTracker.Clear();

                await WriteProblemAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    ErrorCodes.RequestInProgress,
                    "An identical request is already in flight.");
                return;
            }
        }

        // Buffer the response so a successful body can be stored for replay.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }

        await RecordOutcomeAsync(db, userId, method, normalizedPath, key, context, buffer, clock);
    }

    private static async Task RecordOutcomeAsync(
        GongWeiDbContext db,
        Guid userId,
        string method,
        string normalizedPath,
        string key,
        HttpContext context,
        MemoryStream buffer,
        IClock clock)
    {
        var record = await db.IdempotencyRecords.FirstOrDefaultAsync(
            r => r.UserId == userId
                 && r.HttpMethod == method
                 && r.RequestPath == normalizedPath
                 && r.IdempotencyKey == key,
            CancellationToken.None);

        if (record is null)
        {
            return;
        }

        // Only 2xx is replayable. A failed attempt leaves the key free to retry properly.
        var isSuccess = context.Response.StatusCode is >= 200 and < 300;

        if (isSuccess)
        {
            buffer.Position = 0;
            var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();

            record.Status = IdempotencyStatus.Completed;
            record.ResponseStatus = context.Response.StatusCode;
            record.ResponseBody = string.IsNullOrWhiteSpace(body) ? "{}" : body;
            record.CompletedAt = clock.UtcNow;
        }
        else
        {
            record.Status = IdempotencyStatus.Failed;
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Lowercase hex, because <c>request_hash</c> is varchar(128) rather than bytea —
    /// an operator comparing two records by eye is the reason the schema stores text.
    /// </summary>
    private static async Task<string> ComputeRequestHashAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string code,
        string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = code,
            status,
            detail,
            code,
            requestId = context.TraceIdentifier
        });
    }
}
