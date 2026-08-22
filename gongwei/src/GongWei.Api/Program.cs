using System.Threading.RateLimiting;
using GongWei.Api.Http;
using GongWei.Application.Abstractions;
using GongWei.Infrastructure;
using GongWei.Infrastructure.Persistence;
using GongWei.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// IIS hosting: the ASP.NET Core Module launches Kestrel and forwards. Only headers from
// the module are honoured — arbitrary X-Forwarded-* from the internet are not (spec §2.1).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddGongWeiInfrastructure(builder.Configuration);

// Data Protection seals the LINE login attempt payload. The key ring must live outside
// the web root and be shared by every API instance, otherwise an app-pool recycle between
// /auth/line/start and the callback would fail every in-flight login (line_login_v1.1 §8.4).
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];

var dataProtection = builder.Services
    .AddDataProtection()
    // Pinned: the default is derived from the content root, which changes on every
    // release folder, silently invalidating the key ring on deploy.
    .SetApplicationName("GongWeiFuSheng");

if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "DataProtection:KeyRingPath must be configured outside Development. Without it the key " +
        "ring is per-process and every app-pool recycle breaks logins that are mid-flight.");
}

builder.Services
    .AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<SessionCookieOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName,
        options => options.CookieName = builder.Configuration["Session:CookieName"] ?? "gw_session");

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;

        // Nulls are written, not dropped. Every DTO example in api_v1_v1.1 spells its
        // nulls out ("url": null, "appliesToRole": null, "rank": null), and a client that
        // checks for a key's presence rather than its value would read a dropped null as
        // a different thing entirely. Omitting them also makes the OpenAPI document
        // disagree with the responses.
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Native ASP.NET Core OpenAPI. CI snapshots this document and fails the build on an
// unintended breaking change (spec §0).
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "宮闈浮生 GongWeiFuSheng API";
        document.Info.Version = "v1";
        document.Info.Description = "Player API. Cookie session + CSRF; see 後端規格書 v0.8 §7.";
        return Task.CompletedTask;
    });
});

// CORS is only for the GitHub Pages player front end. The admin site is same-origin on
// its own IIS site and gets no CORS at all (spec §11).
const string PlayerCorsPolicy = "player-frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(PlayerCorsPolicy, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        // Never "*" together with credentials — the browser would refuse anyway, and the
        // combination is exactly what the spec forbids. Origins are listed exactly; a
        // suffix match would accept an attacker's evil-miglow.vip (line_login_v1.1 §5.3).
        policy.WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithHeaders("Content-Type", "X-CSRF-Token", "Idempotency-Key", "If-Match")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithExposedHeaders("ETag", "Idempotency-Replayed");
    });
});

// Six separate buckets, as the spec requires: auth, read, post, economy, reproduction, admin.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", PartitionByCaller(10, TimeSpan.FromMinutes(1)));
    options.AddPolicy("read", PartitionByCaller(300, TimeSpan.FromMinutes(1)));
    options.AddPolicy("post", PartitionByCaller(30, TimeSpan.FromMinutes(1)));
    options.AddPolicy("economy", PartitionByCaller(20, TimeSpan.FromMinutes(1)));
    options.AddPolicy("reproduction", PartitionByCaller(10, TimeSpan.FromMinutes(1)));
    options.AddPolicy("admin", PartitionByCaller(120, TimeSpan.FromMinutes(1)));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            CallerKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Readiness proves the things a login actually needs, not just that the process is up
// (line_login_v1.1 §8). "config" is tagged separately so an operator can see at a glance
// whether a red readiness means a broken database or an unfinished deploy.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GongWeiDbContext>("database")
    .AddCheck<LineLoginConfigurationCheck>("line-login-config", tags: ["config"])
    .AddCheck<DataProtectionCheck>("data-protection", tags: ["config"]);

var app = builder.Build();

app.UseForwardedHeaders();

// Serilog's default RequestPath is the raw target, which on /auth/line/callback carries
// the authorization code and the full state. line_login_v1.1 §6 forbids logging either,
// so the template uses the path alone.
//
// The framework's own "Request starting/finished" lines log the raw URL as well. They are
// silenced by holding Microsoft.AspNetCore.Hosting.Diagnostics at Warning in both
// appsettings files — do not raise that to Information, in Development either.
// (Serilog rejects "//" comment keys inside its own config sections, which is why this
// note lives here rather than next to the setting.)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPathOnly} responded {StatusCode} in {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set("RequestPathOnly", httpContext.Request.Path.Value);
});
app.UseExceptionHandler();

// Published in every environment, not just Development: the front end is a separate
// team on a separate repo, and the OpenAPI document is how they see the real request and
// response shapes rather than reading them out of the spec by hand. It describes only
// routes that actually exist, which also makes "is this endpoint built yet" answerable.
//
// It carries no secret — the channel secret is never a parameter, and settings are not
// serialised into the document (api_v1_v1.1 §2.1).
app.MapOpenApi("/api/v1/openapi.json").AllowAnonymous();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(PlayerCorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<CsrfMiddleware>();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers().RequireRateLimiting("read");

// Under /api/v1 like every other endpoint. api_v1_v1.1 §1 states the table's paths are
// relative to the Base URL and forbids publishing a second, unversioned set — so these
// move rather than being duplicated.
//
// Liveness answers "is the process up"; readiness also proves the database, the Data
// Protection key ring and the required settings are usable (line_login_v1.1 §8).
app.MapHealthChecks("/api/v1/health/live", new()
{
    Predicate = _ => false
});
// Names each failing check in the body. The endpoint is only reachable through
// Cloudflare, and the checks are written so their messages carry no secret material.
app.MapHealthChecks("/api/v1/health/ready", new()
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
    }
});

app.MapGet("/api/v1/meta", () => Results.Ok(new
{
    name = "GongWeiFuSheng API",
    apiVersion = "v1",
    rulesVersion = "v1.1",
    environment = app.Environment.EnvironmentName,
    csrfHeader = CsrfMiddleware.HeaderName,
    idempotencyHeader = IdempotencyMiddleware.HeaderName,
    loginStartPath = "/api/v1/auth/line/start"
})).AllowAnonymous();

app.Run();

static Func<HttpContext, RateLimitPartition<string>> PartitionByCaller(int permits, TimeSpan window) =>
    context => RateLimitPartition.GetFixedWindowLimiter(
        CallerKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window });

// Authenticated callers are limited per account; anonymous ones per source address, so
// one noisy IP behind NAT cannot lock out a signed-in player.
static string CallerKey(HttpContext context) =>
    context.User.FindFirst(GongWeiClaims.UserId)?.Value
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "anonymous";

/// <summary>Exposed so the API integration tests can spin up the real pipeline.</summary>
public partial class Program;
