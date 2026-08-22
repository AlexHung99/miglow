using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Operations;
using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Worker;

/// <summary>
/// Drains the transactional outbox. Claims a batch with
/// <c>FOR UPDATE SKIP LOCKED</c> so several workers can run without stepping on each
/// other, and gives up permanently after max_attempts rather than retrying forever (spec §10).
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    /// <summary>
    /// Retry ceiling. Held here rather than per row because v1.1's outbox table has no
    /// max_attempts column — the limit is a policy of this dispatcher, not of the data.
    /// </summary>
    private const int MaxAttempts = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox dispatcher started");

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatched = await DispatchBatchAsync(stoppingToken);

                if (dispatched == BatchSize)
                {
                    // A full batch means there is probably more waiting; skip the wait.
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch cycle failed; retrying next tick");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Outbox dispatcher stopped");
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<GongWeiDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>().ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var now = clock.UtcNow;

        // v1.1's outbox has no status column: pending simply means processed_at IS NULL,
        // which is also what ix_outbox_messages_pending is filtered on.
        var claimed = await db.OutboxMessages
            .FromSqlRaw(
                """
                SELECT * FROM game.outbox_messages
                WHERE processed_at IS NULL
                  AND available_at <= {0}
                ORDER BY available_at, id
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """,
                now, BatchSize)
            .ToListAsync(ct);

        foreach (var message in claimed)
        {
            message.AttemptCount += 1;

            try
            {
                foreach (var handler in handlers.Where(h => h.CanHandle(message.Topic)))
                {
                    await handler.HandleAsync(message, ct);
                }

                message.ProcessedAt = clock.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.LastError = Truncate(ex.Message, 2000);

                if (message.AttemptCount >= MaxAttempts)
                {
                    // Give up rather than retry forever: an infinite loop is worse than a
                    // visible failure (spec §10). With no dead-letter column, the message
                    // is closed off with its error still attached — the operator query is
                    // "processed_at IS NOT NULL AND last_error IS NOT NULL".
                    message.ProcessedAt = clock.UtcNow;

                    logger.LogError(ex,
                        "Outbox message {Id} ({Topic}) gave up after {Attempts} attempts",
                        message.Id, message.Topic, message.AttemptCount);
                }
                else
                {
                    message.AvailableAt = clock.UtcNow.Add(message.NextBackoff());

                    logger.LogWarning(ex,
                        "Outbox message {Id} ({Topic}) failed, retrying at {AvailableAt}",
                        message.Id, message.Topic, message.AvailableAt);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return claimed.Count;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

/// <summary>
/// Reacts to one kind of outbox message. Handlers must be idempotent: a message can be
/// delivered again if the worker dies between the side effect and the commit.
/// </summary>
public interface IOutboxMessageHandler
{
    bool CanHandle(string messageType);

    Task HandleAsync(OutboxMessage message, CancellationToken ct);
}

/// <summary>
/// Turns domain events into in-app notifications. LINE push is deliberately not wired
/// up: the spec keeps the LINE group for human conversation only (spec §1.1).
/// </summary>
public sealed class NotificationOutboxHandler(
    GongWeiDbContext db,
    IClock clock,
    IJsonSerializer json,
    ILogger<NotificationOutboxHandler> logger) : IOutboxMessageHandler
{
    private static readonly Dictionary<string, (string Kind, string Title)> Templates = new()
    {
        ["character_application.needs_revision"] = ("character_application", "建角申請需要補件"),
        ["character_application.rejected"] = ("character_application", "建角申請未通過"),
        ["character.created"] = ("character_application", "角色已建立"),
        ["audience.resolved"] = ("reproduction", "侍奉結果已公布"),
        ["pregnancy.due"] = ("reproduction", "臨盆在即"),
        ["pregnancy.miscarried"] = ("reproduction", "懷孕中止"),
        ["birth.drawn"] = ("reproduction", "皇嗣誕生")
    };

    public bool CanHandle(string messageType) => Templates.ContainsKey(messageType);

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        var (kind, title) = Templates[message.Topic];
        var payload = json.Deserialize<Dictionary<string, object?>>(message.Payload);

        if (payload is null || !payload.TryGetValue("userId", out var rawUserId))
        {
            // Not every message names a user; those are for other handlers.
            logger.LogDebug("Outbox message {Id} carries no userId; nothing to notify", message.Id);
            return;
        }

        if (!Guid.TryParse(rawUserId?.ToString(), out var userId))
        {
            return;
        }

        // Re-delivery must not produce a duplicate notification.
        var alreadySent = await db.Notifications.AnyAsync(
            n => n.UserId == userId
                 && n.NotificationType == kind
                 && n.Title == title
                 && n.CreatedAt >= message.OccurredAt,
            ct);

        if (alreadySent)
        {
            return;
        }

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            NotificationType = kind,
            Title = title,
            Payload = message.Payload,
            CreatedAt = clock.UtcNow
        });
    }
}
