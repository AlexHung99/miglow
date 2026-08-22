using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Infrastructure;
using GongWei.Worker;
using GongWei.Worker.Jobs;
using Serilog;

// Runs as a Windows Service: IIS application pools recycle and are not a reliable place
// for scheduling, so the worker is hosted separately (spec §2.3).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "GongWeiWorker");

builder.Services.AddSerilog((services, config) =>
    config.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(services));

builder.Services.AddGongWeiInfrastructure(builder.Configuration);

// The worker acts as the system, not as a signed-in admin: it holds no admin role and
// cannot execute anything that needs two-person review.
builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();

builder.Services.AddScoped<IScheduledJobHandler, SessionCleanupJob>();
builder.Services.AddScoped<IScheduledJobHandler, IdempotencyCleanupJob>();
builder.Services.AddScoped<IScheduledJobHandler, LoginAttemptCleanupJob>();
builder.Services.AddScoped<IScheduledJobHandler, EventStateTransitionJob>();
builder.Services.AddScoped<IScheduledJobHandler, PregnancyDueJob>();
builder.Services.AddScoped<IScheduledJobHandler, StatusEffectResolveJob>();
builder.Services.AddScoped<IScheduledJobHandler, MediaPurgeJob>();

builder.Services.AddScoped<IOutboxMessageHandler, NotificationOutboxHandler>();

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<ScheduledJobRunner>();

var host = builder.Build();
host.Run();

/// <summary>
/// The worker's identity. Background jobs are audited as system actions with no admin
/// role, so nothing scheduled can bypass the two-person review rules (spec §9.2).
/// </summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public bool IsAuthenticated => false;

    public IReadOnlySet<AdminRole> AdminRoles { get; } = new HashSet<AdminRole>();

    public string? RequestId => null;

    public string? IpAddress => null;

    public string? UserAgent => "GongWei.Worker";

    public bool HasRole(AdminRole role) => false;

    public Guid RequireUserId() =>
        throw new InvalidOperationException(
            "A background job tried to act as a user. Jobs must not run user-scoped use cases.");

    public void RequireRole(params AdminRole[] anyOf) =>
        throw new DomainException(
            ErrorCodes.Forbidden,
            "Background jobs hold no admin role.",
            DomainErrorKind.Forbidden);
}
