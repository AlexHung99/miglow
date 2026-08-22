using GongWei.Application.Identity;
using GongWei.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GongWei.Api.Http;

/// <summary>
/// Readiness check for the LINE Login configuration (line_login_v1.1 §8).
///
/// Without this the site looks perfectly healthy — /meta answers, the database is
/// reachable — right up until a player presses the login button and gets a 500. A
/// deployment that cannot sign anyone in is not ready, and readiness is where that
/// belongs.
///
/// The secret is only ever tested for presence. Its value is never read into a message,
/// never logged, and never returned in the health response (§11).
/// </summary>
public sealed class LineLoginConfigurationCheck(IOptions<LineLoginOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.ChannelId))
        {
            missing.Add("LineLogin:ChannelId");
        }

        if (string.IsNullOrWhiteSpace(settings.ChannelSecret))
        {
            missing.Add("LineLogin:ChannelSecret");
        }

        if (string.IsNullOrWhiteSpace(settings.RedirectUri))
        {
            missing.Add("LineLogin:RedirectUri");
        }

        if (missing.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Not configured: {string.Join(", ", missing)}. " +
                "Set these through IIS configuration; see deploy/iis/set-app-settings.ps1."));
        }

        // The redirect_uri must match the LINE console entry byte for byte, and a mismatch
        // produces an opaque failure at token exchange rather than at the redirect. Catching
        // the obvious shape error here saves that debugging session.
        if (!Uri.TryCreate(settings.RedirectUri, UriKind.Absolute, out var redirect)
            || !redirect.AbsolutePath.EndsWith("/auth/line/callback", StringComparison.Ordinal))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "LineLogin:RedirectUri must be an absolute URL ending in /auth/line/callback."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}

/// <summary>
/// Readiness check for the Data Protection key ring.
///
/// A key ring that cannot be written is the failure mode that produces the worst symptom:
/// every login works until the process recycles, then every attempt in flight fails to
/// unseal and the cause is invisible. Proving a protector can round-trip is cheap.
/// </summary>
public sealed class DataProtectionCheck(IDataProtectionProvider provider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Same purpose string the login flow uses, so this exercises the real key.
            var protector = provider.CreateProtector(LineLoginService.ProtectionPurpose);

            const string probe = "readiness";
            var roundTripped = protector.Unprotect(protector.Protect(probe));

            return Task.FromResult(roundTripped == probe
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Data Protection round trip returned different data."));
        }
        catch (Exception ex)
        {
            // The message can name key ring paths, which is fine for an operator on a
            // private readiness endpoint but must not carry key material — it does not.
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Data Protection key ring is not usable.", ex));
        }
    }
}
