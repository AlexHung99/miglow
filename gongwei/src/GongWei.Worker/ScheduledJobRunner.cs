using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Operations;
using GongWei.Infrastructure.Persistence;
using GongWei.Worker.Jobs;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Worker;

/// <summary>
/// Claims due jobs with a lease, runs them and records the outcome in job_runs.
/// If this process dies the lease expires and another worker takes over, which is why
/// every handler has to be safe to re-run (spec §10).
/// </summary>
public sealed class ScheduledJobRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledJobRunner> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int MaxConsecutiveFailuresBeforeAlert = 5;

    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled job runner {WorkerId} started", _workerId);

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled job poll failed");
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

        logger.LogInformation("Scheduled job runner {WorkerId} stopped", _workerId);
    }

    private async Task RunDueJobsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<GongWeiDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var handlers = scope.ServiceProvider
            .GetServices<IScheduledJobHandler>()
            .ToDictionary(h => h.JobKey, StringComparer.Ordinal);

        var now = clock.UtcNow;

        // Interval jobs only. Domain-event jobs (monthly stipend, action point reset)
        // are triggered by the world clock advancing, not by wall time.
        var due = await db.ScheduledJobs
            .Where(j => j.IsEnabled
                        && j.ScheduleKind == ScheduleKind.Interval
                        && j.NextRunAt <= now
                        && (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
            .ToListAsync(ct);

        foreach (var job in due)
        {
            if (!handlers.TryGetValue(job.JobKey, out var handler))
            {
                continue;
            }

            if (!await TryAcquireLeaseAsync(db, job, clock, ct))
            {
                continue;
            }

            await ExecuteWithLeaseAsync(scope.ServiceProvider, job, handler, ct);
        }
    }

    /// <summary>
    /// Conditional update: only the worker whose UPDATE actually changed a row owns the
    /// lease, so two workers polling at the same instant cannot both run the job.
    /// </summary>
    private async Task<bool> TryAcquireLeaseAsync(
        GongWeiDbContext db,
        ScheduledJob job,
        IClock clock,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var leaseUntil = now.Add(LeaseDuration);

        var claimed = await db.ScheduledJobs
            .Where(j => j.Id == job.Id
                        && j.Version == job.Version
                        && (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(
                j => j.SetProperty(x => x.LeaseOwner, _workerId)
                      .SetProperty(x => x.LeaseExpiresAt, leaseUntil)
                      .SetProperty(x => x.UpdatedAt, now)
                      .SetProperty(x => x.Version, x => x.Version + 1),
                ct);

        return claimed == 1;
    }

    private async Task ExecuteWithLeaseAsync(
        IServiceProvider services,
        ScheduledJob job,
        IScheduledJobHandler handler,
        CancellationToken ct)
    {
        var db = services.GetRequiredService<GongWeiDbContext>();
        var clock = services.GetRequiredService<IClock>();

        var run = new JobRun
        {
            ScheduledJobId = job.Id,
            JobKey = job.JobKey,
            Status = JobRunStatus.Running,
            WorkerId = _workerId,
            StartedAt = clock.UtcNow
        };

        db.JobRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            var processed = await handler.RunAsync(ct);

            run.Status = JobRunStatus.Succeeded;
            run.ProcessedCount = processed;
            run.FinishedAt = clock.UtcNow;

            await ReleaseLeaseAsync(db, job, clock, succeeded: true, ct);

            if (processed > 0)
            {
                logger.LogInformation("Job {JobKey} processed {Count} items", job.JobKey, processed);
            }
        }
        catch (Exception ex)
        {
            run.Status = JobRunStatus.Failed;
            run.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            run.FinishedAt = clock.UtcNow;

            await ReleaseLeaseAsync(db, job, clock, succeeded: false, ct);

            logger.LogError(ex, "Job {JobKey} failed", job.JobKey);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ReleaseLeaseAsync(
        GongWeiDbContext db,
        ScheduledJob job,
        IClock clock,
        bool succeeded,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var interval = TimeSpan.FromSeconds(job.IntervalSeconds ?? 60);

        // A repeatedly failing job backs off instead of hammering the database, and is
        // reported so an admin sees it rather than it silently spinning (spec §10).
        var failures = succeeded ? 0 : job.ConsecutiveFailures + 1;
        var nextRun = succeeded
            ? now.Add(interval)
            : now.Add(TimeSpan.FromSeconds(Math.Min(600, interval.TotalSeconds * Math.Pow(2, failures))));

        await db.ScheduledJobs
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(
                j => j.SetProperty(x => x.LeaseOwner, (string?)null)
                      .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                      .SetProperty(x => x.LastRunAt, now)
                      .SetProperty(x => x.NextRunAt, nextRun)
                      .SetProperty(x => x.ConsecutiveFailures, failures)
                      .SetProperty(x => x.UpdatedAt, now)
                      .SetProperty(x => x.Version, x => x.Version + 1),
                ct);

        if (failures >= MaxConsecutiveFailuresBeforeAlert)
        {
            logger.LogCritical(
                "Job {JobKey} has failed {Failures} times in a row and needs attention",
                job.JobKey, failures);
        }
    }
}
