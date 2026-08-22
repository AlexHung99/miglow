namespace GongWei.Domain.World;

/// <summary>
/// Validates the external support link before it is shown to a player
/// (api_v1_v1.1 §14.5).
///
/// This is an allowlist of one platform and one URL shape, matched against the same
/// pattern the setting's <c>validation_schema</c> enforces in the database. It exists
/// separately in code because the setting row is written by admins and read by the public
/// endpoint: validating only on write would leave a row edited directly in SQL, or seeded
/// from an older script, able to point players anywhere.
///
/// A URL that fails is reported as "not configured" rather than being replaced with the
/// platform's home page — sending a player to buymeacoffee.com with no creator would look
/// like the site endorsing a stranger's page.
/// </summary>
public static class SupportLinkPolicy
{
    public const string SettingKey = "support.buy_me_a_coffee";

    public const string AllowedHost = "buymeacoffee.com";

    public const string DefaultLabel = "請我們喝杯咖啡";

    public const int MaxLabelLength = 30;

    /// <summary>
    /// Returns the URL to publish, or null when it is unusable. The value is rebuilt from
    /// the parsed components so nothing the parser ignored survives into a response.
    /// </summary>
    public static string? Normalise(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (candidate.Any(char.IsControl) || candidate.Contains('\\'))
        {
            return null;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        // Exact host, never a suffix test: "buymeacoffee.com.evil.example" ends with the
        // allowed host and would pass anything looser.
        if (!string.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return null;
        }

        // Exactly one path segment — the creator name. "/" alone is the platform home page
        // and is deliberately rejected.
        var creator = uri.AbsolutePath.Trim('/');

        if (creator.Length == 0
            || creator.Length > 100
            || !creator.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            return null;
        }

        return $"https://{AllowedHost}/{creator}";
    }

    /// <summary>Trims a label to the schema's 30-character ceiling, falling back to the default.</summary>
    public static string NormaliseLabel(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return DefaultLabel;
        }

        var trimmed = candidate.Trim();

        return trimmed.Length <= MaxLabelLength ? trimmed : trimmed[..MaxLabelLength];
    }
}
