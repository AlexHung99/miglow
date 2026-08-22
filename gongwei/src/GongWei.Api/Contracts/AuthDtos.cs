using GongWei.Domain.Characters;
using GongWei.Domain.Common;

namespace GongWei.Api.Contracts;

// API DTOs never expose EF entities directly, and private/admin fields live on separate
// shapes from the public ones (api_v1_v1.1 §1).

/// <summary>
/// <c>MeDto</c> (api_v1_v1.1 §14.1) — the first call the SPA makes after the login
/// redirect lands.
///
/// Deliberately absent: lineUserId, any LINE or session token, past character ids, and
/// admin notes. <see cref="CharacterState"/> is the field the front end routes on, and it
/// is always present even when <see cref="Character"/> is null.
/// </summary>
public sealed record MeResponse(
    MeUserResponse User,
    string CharacterState,
    MeCharacterResponse? Character,
    IReadOnlyList<string> AdminRoles,
    int UnreadNotificationCount,
    IReadOnlyList<string> PendingActions);

public sealed record MeUserResponse(
    Guid Id,
    string DisplayName,
    string Status,
    IReadOnlyDictionary<string, object> Preferences,
    long Version);

public sealed record MeRankResponse(Guid Id, string Name);

public sealed record MeCharacterResponse(
    Guid Id,
    string DisplayName,
    string Role,
    string Status,
    string? PortraitUrl,
    MeRankResponse? Rank,
    long Version)
{
    public static MeCharacterResponse From(Character c, string? portraitUrl) =>
        new(
            c.Id,
            // The stored name is split; the display form is assembled here rather than
            // duplicated on the entity, because court naming rules belong to presentation.
            string.IsNullOrEmpty(c.FamilyName) ? c.GivenName : $"{c.FamilyName}{c.GivenName}",
            EnumNaming.ToDbValue(c.Role),
            EnumNaming.ToDbValue(c.Status),
            portraitUrl,
            c.Rank is null ? null : new MeRankResponse(c.Rank.Id, c.Rank.DisplayName),
            c.Version);
}

/// <summary>
/// The six values §14.1 fixes for <c>characterState</c>. A player with no character at
/// all reports <c>none</c> rather than omitting the field.
/// </summary>
public static class CharacterStateValues
{
    public const string None = "none";

    public static string From(CharacterStatus? status) => status switch
    {
        null => None,
        CharacterStatus.WaitingBirth => "waiting_birth",
        CharacterStatus.Active => "active",
        CharacterStatus.Paused => "paused",
        CharacterStatus.Suspended => "suspended",
        CharacterStatus.Dead => "dead",
        _ => None
    };
}

/// <summary>
/// <c>CsrfTokenDto</c> (api_v1_v1.1 §14.1A). The header name is not part of this shape —
/// it is published once through <c>GET /meta</c>.
/// </summary>
public sealed record CsrfTokenResponse(string Token, DateTimeOffset ExpiresAt);
