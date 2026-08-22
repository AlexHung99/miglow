using System.Text.Json;
using GongWei.Domain.World;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Controllers;

/// <summary>
/// <c>PublicSupportSettingDto</c> (api_v1_v1.1 §14.5).
///
/// Carries no payment amount, membership, webhook or game-account mapping — the site
/// links out and holds nothing about the transaction.
/// </summary>
public sealed record PublicSupportSettingResponse(
    bool Enabled,
    bool Configured,
    string? Url,
    string Label,
    long Version);

/// <summary>
/// Settings a player may read without a session. Only rows explicitly marked
/// <c>is_public</c> are ever served here.
/// </summary>
[ApiController]
[Route("api/v1/public-settings")]
public sealed class PublicSettingsController(GongWeiDbContext db) : ControllerBase
{
    /// <summary>
    /// Backs the support button in the top-right corner.
    ///
    /// Three states the front end has to tell apart:
    ///   enabled=false                  hide the button entirely
    ///   enabled=true, configured=false show the button, disable the outbound CTA
    ///   enabled=true, configured=true  show the button and the link
    /// </summary>
    [HttpGet("support")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicSupportSettingResponse>> Support(CancellationToken ct)
    {
        var setting = await db.GameSettings
            .AsNoTracking()
            .Where(s => s.SettingKey == SupportLinkPolicy.SettingKey && s.IsPublic)
            .Select(s => new { s.PublishedValue, s.Version })
            .FirstOrDefaultAsync(ct);

        // The row arrives with seed_rules_v1.1.sql, which cannot run before the first
        // super admin exists. Until then the endpoint answers "off" rather than 404, so
        // the front end has one shape to handle instead of two.
        if (setting is null)
        {
            return Ok(new PublicSupportSettingResponse(
                Enabled: false,
                Configured: false,
                Url: null,
                Label: SupportLinkPolicy.DefaultLabel,
                Version: 0));
        }

        var value = Parse(setting.PublishedValue);

        // Re-validated on read, not trusted from the row: see SupportLinkPolicy for why.
        var url = SupportLinkPolicy.Normalise(value.Url);

        return Ok(new PublicSupportSettingResponse(
            Enabled: value.Enabled,
            Configured: url is not null,
            Url: url,
            Label: SupportLinkPolicy.NormaliseLabel(value.Label),
            Version: setting.Version));
    }

    /// <summary>
    /// Reads the jsonb by hand rather than deserialising into a type, so a setting saved
    /// with an unexpected shape degrades to "off" instead of throwing on a public endpoint.
    /// </summary>
    private static (bool Enabled, string? Url, string? Label) Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return (false, null, null);
            }

            var enabled = root.TryGetProperty("enabled", out var enabledElement)
                          && enabledElement.ValueKind == JsonValueKind.True;

            var url = root.TryGetProperty("url", out var urlElement)
                      && urlElement.ValueKind == JsonValueKind.String
                ? urlElement.GetString()
                : null;

            var label = root.TryGetProperty("label", out var labelElement)
                        && labelElement.ValueKind == JsonValueKind.String
                ? labelElement.GetString()
                : null;

            return (enabled, url, label);
        }
        catch (JsonException)
        {
            return (false, null, null);
        }
    }
}
