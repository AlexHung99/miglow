using GongWei.Admin.Security;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// The key ring lives outside the deployment directory so a redeploy does not invalidate
// every admin cookie, and is protected at rest (spec §2.3).
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];

if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    builder.Services
        .AddDataProtection()
        .SetApplicationName("GongWei.Admin")
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, AdminCurrentUser>();

builder.Services.AddGongWeiInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // A different cookie name and path from the player API, so a player cookie is
        // useless here and vice versa (spec §11).
        options.Cookie.Name = "gw_admin_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/SignIn";
        options.AccessDeniedPath = "/Account/Denied";

        // Short idle timeout for admin sessions (spec §11).
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(AdminPolicies.ReviewCharacters, p => p.RequireAdminRole(
        AdminRole.CharacterReviewer, AdminRole.SuperAdmin))
    .AddPolicy(AdminPolicies.ManageGameplay, p => p.RequireAdminRole(
        AdminRole.GameMaster, AdminRole.SuperAdmin))
    .AddPolicy(AdminPolicies.ManageEconomy, p => p.RequireAdminRole(
        AdminRole.EconomyManager, AdminRole.SuperAdmin))
    .AddPolicy(AdminPolicies.ReadAudit, p => p.RequireAdminRole(
        AdminRole.Auditor, AdminRole.SuperAdmin))
    .AddPolicy(AdminPolicies.ReviewApprovals, p => p.RequireAdminRole(
        AdminRole.GameMaster, AdminRole.EconomyManager, AdminRole.Moderator,
        AdminRole.SystemConfigManager, AdminRole.SuperAdmin));

// AntiForgery on every non-GET, since admin forms are same-origin and get no CORS.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToFolder("/Account");
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "gw_admin_csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHealthChecks("/health/ready");

app.Run();
