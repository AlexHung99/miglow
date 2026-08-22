using GongWei.Application.Abstractions;
using GongWei.Application.Characters;
using GongWei.Application.Common;
using GongWei.Application.Economy;
using GongWei.Application.Events;
using GongWei.Application.Identity;
using GongWei.Application.Operations;
using GongWei.Application.Reproduction;
using GongWei.Infrastructure.Persistence;
using GongWei.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GongWei.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires persistence, the shared services and every Application use case. The API,
    /// the Admin site and the Worker all call this — they must never reach past the
    /// Application layer to touch EF entities directly (spec §2.2).
    /// </summary>
    public static IServiceCollection AddGongWeiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Key name fixed by implementation_bootstrap_v1.1 §6: ConnectionStrings__GameDb.
        var connectionString = configuration.GetConnectionString("GameDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:GameDb is not configured. Provide it through user-secrets in " +
                "development or through IIS/Windows configuration in production — never in appsettings.json.");

        services.AddDbContext<GongWeiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", GongWeiDbContext.SchemaName);

                // EnableRetryOnFailure is deliberately NOT set.
                //
                // Npgsql's retrying execution strategy refuses to run alongside a
                // user-initiated transaction, and every write path in this application
                // opens one — the spec's fixed lock ordering and FOR UPDATE flows depend
                // on it. With retries on, all nineteen of those paths throw
                // "does not support user-initiated transactions" at runtime; that is what
                // broke the first real LINE login, after the token exchange had already
                // succeeded.
                //
                // The EF-blessed alternative is to wrap each transaction in
                // CreateExecutionStrategy().ExecuteAsync. It is rejected here for two
                // reasons. Missing one call site produces a 500 that only appears in
                // production, and the strategy replays the whole block — which for the
                // birth draw would re-roll a CSPRNG result the spec requires to be
                // auditable (§6.4).
                //
                // The trade is cheap: PostgreSQL runs on 127.0.0.1, so there is no network
                // to be transiently unavailable. If the database ever moves off-box,
                // re-enable this AND wrap every transaction in an execution strategy —
                // both, or neither.
            });
        });

        services.AddScoped<IGongWeiDb>(sp => sp.GetRequiredService<GongWeiDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRandomProvider, CryptoRandomProvider>();
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.AddSingleton<IImageProcessor, ImageSharpPortraitProcessor>();

        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();

        services.Configure<MediaStorageOptions>(configuration.GetSection("MediaStorage"));
        services.AddSingleton<IMediaStorage, FileSystemMediaStorage>();

        services.AddScoped<SessionService>();
        services.AddScoped<ISessionIssuer>(sp => sp.GetRequiredService<SessionService>());

        services.Configure<LineLoginOptions>(configuration.GetSection("LineLogin"));

        // 10s timeout, fixed by line_login_v1.1 §4.2 — a slow LINE must not hold a
        // request thread long enough to exhaust the pool.
        services.AddHttpClient<ILineLoginClient, LineLoginClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));

        services.AddScoped<ILineLoginAttemptStore, LineLoginAttemptStore>();
        services.AddSingleton<IPayloadProtector, DataProtectionPayloadProtector>();

        return services.AddGongWeiUseCases();
    }

    public static IServiceCollection AddGongWeiUseCases(this IServiceCollection services)
    {
        services.AddScoped<GameSettingsReader>();
        services.AddScoped<LineLoginService>();
        services.AddScoped<CharacterApplicationService>();
        services.AddScoped<PortraitService>();
        services.AddScoped<ReproductionService>();
        services.AddScoped<EconomyService>();
        services.AddScoped<EventPostService>();
        services.AddScoped<ApprovalService>();

        return services;
    }
}
