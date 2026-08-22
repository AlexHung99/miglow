using System.Text.Json;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Common;

/// <summary>
/// The allowlisted setting keys the admin site may edit (§6.9). Anything not listed here
/// simply has no editor and no API — there is no free-form key/value store.
/// </summary>
public static class SettingKeys
{
    public const string SupportBuyMeACoffee = "support.buy_me_a_coffee";
    public const string EventPostReviewRequired = "event.post_review_required";
    public const string ApplicationScoreWeights = "character.application_score_weights";
    public const string ChapterAdvanceMode = "world.chapter_advance_mode";
}

/// <summary>
/// Reads published game settings. Rules that remain product decisions (§16) come from
/// here rather than from constants in controllers, so they change without a deployment.
/// </summary>
public sealed class GameSettingsReader(IGongWeiDb db)
{
    public async Task<T> GetAsync<T>(string key, T fallback, CancellationToken ct = default)
    {
        var json = await db.GameSettings
            .Where(s => s.SettingKey == key)
            .Select(s => s.PublishedValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? fallback;
        }
        catch (JsonException)
        {
            // A malformed published value is a data bug, not a request error — fall back
            // rather than failing every player request until an admin notices.
            return fallback;
        }
    }

    /// <summary>Public settings are readable without a session, e.g. the support link.</summary>
    public async Task<string?> GetPublicRawAsync(string key, CancellationToken ct = default) =>
        await db.GameSettings
            .Where(s => s.SettingKey == key && s.IsPublic)
            .Select(s => s.PublishedValue)
            .FirstOrDefaultAsync(ct);
}

/// <summary>
/// Validates a setting value against the JSON Schema fragment stored beside it.
/// Deliberately narrow: the admin site can only touch allowlisted keys, and only with
/// values of the declared shape (§6.9).
/// </summary>
public static class SettingValueValidator
{
    public static void Validate(string schemaJson, string valueJson)
    {
        JsonElement schema, value;

        try
        {
            schema = JsonDocument.Parse(schemaJson).RootElement;
            value = JsonDocument.Parse(valueJson).RootElement;
        }
        catch (JsonException ex)
        {
            throw DomainException.Validation($"JSON 格式錯誤：{ex.Message}");
        }

        if (!schema.TryGetProperty("type", out var typeElement))
        {
            throw DomainException.Validation("設定的驗證 Schema 未宣告 type。");
        }

        var expected = typeElement.GetString();
        var matches = expected switch
        {
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "string" => value.ValueKind == JsonValueKind.String,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => false
        };

        if (!matches)
        {
            throw DomainException.Validation($"設定值型別應為 {expected}，實際為 {value.ValueKind}。");
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            var number = value.GetDouble();

            if (schema.TryGetProperty("minimum", out var min) && number < min.GetDouble())
            {
                throw DomainException.Validation($"設定值不得小於 {min.GetDouble()}。");
            }

            if (schema.TryGetProperty("maximum", out var max) && number > max.GetDouble())
            {
                throw DomainException.Validation($"設定值不得大於 {max.GetDouble()}。");
            }
        }
    }
}

/// <summary>
/// The Buy Me a Coffee link (§6.13). The URL host must be exactly buymeacoffee.com over
/// HTTPS; anything else disables the external call-to-action rather than rendering an
/// arbitrary outbound link.
/// </summary>
public sealed record SupportSetting(bool Enabled, string? Url, string? Label)
{
    private const string AllowedHost = "buymeacoffee.com";

    public bool IsConfigured => Enabled && IsAllowedUrl(Url);

    public string? SafeUrl => IsConfigured ? Url : null;

    public static bool IsAllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Trim('/').Length > 0; // a creator slug is required
}
