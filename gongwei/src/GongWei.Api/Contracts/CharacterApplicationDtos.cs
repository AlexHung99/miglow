using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using GongWei.Application.Characters;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;

namespace GongWei.Api.Contracts;

/// <summary>
/// <c>CreateApplicationRequest</c> / <c>UpdateApplicationRequest</c> (api_v1_v1.1 §13.1).
///
/// Every field is optional at this layer on purpose: a Draft may be saved incomplete and
/// repeatedly, and full validation happens on submit. Enforcing lengths here would stop a
/// player saving a half-written biography, which is the opposite of what §13.1 asks for.
///
/// <c>sex</c> is deliberately absent — it is derived from <c>role</c> so the two can never
/// disagree.
/// </summary>
public sealed record ApplicationFormRequest(
    [property: Required] string Role,
    string? FamilyName,
    string? GivenName,
    string? CourtesyName,
    string? BirthDateLabel,
    short? Age,
    string? Appearance,
    string? Biography,
    string? Personality,
    string? Strengths,
    string? Weaknesses,
    string? Likes,
    string? Dislikes,
    Guid? PortraitId,
    Guid? PlayerPortraitSubmissionId,
    JsonElement? FormData)
{
    /// <summary>
    /// Maps to the Application layer's input. The role string is parsed here rather than
    /// bound as an enum so an unknown value produces VALIDATION_FAILED with a usable
    /// message instead of a model-binding 400 the front end cannot interpret.
    /// </summary>
    public ApplicationFormInput ToInput()
    {
        if (!EnumNaming.TryParse<CharacterRole>(Role, out var role))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["role"] = ["角色類型必須是 consort、prince 或 princess。"]
            });
        }

        return new ApplicationFormInput(
            Role: role,
            FamilyName: Trim(FamilyName) ?? string.Empty,
            GivenName: Trim(GivenName) ?? string.Empty,
            CourtesyName: Trim(CourtesyName),
            BirthDateLabel: Trim(BirthDateLabel),
            Age: Age,
            Appearance: Trim(Appearance) ?? string.Empty,
            Biography: Trim(Biography) ?? string.Empty,
            Personality: Trim(Personality) ?? string.Empty,
            Strengths: Trim(Strengths) ?? string.Empty,
            Weaknesses: Trim(Weaknesses) ?? string.Empty,
            Likes: Trim(Likes) ?? string.Empty,
            Dislikes: Trim(Dislikes) ?? string.Empty,
            PortraitId: PortraitId,
            PlayerPortraitSubmissionId: PlayerPortraitSubmissionId,
            FormDataJson: FormData?.ValueKind == JsonValueKind.Object
                ? FormData.Value.GetRawText()
                : null);
    }

    /// <summary>§13.1: all text has leading and trailing whitespace removed.</summary>
    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary><c>SubmitApplicationRequest</c> — the version the player believes they are submitting.</summary>
public sealed record SubmitApplicationRequest(long? ExpectedVersion);

/// <summary><c>CharacterApplicationDto</c>. Reviewer identity is never exposed to the player.</summary>
public sealed record CharacterApplicationResponse(
    Guid Id,
    string Role,
    string Sex,
    string? FamilyName,
    string? GivenName,
    string? CourtesyName,
    string? BirthDateLabel,
    short? Age,
    string? Appearance,
    string? Biography,
    string? Personality,
    string? Strengths,
    string? Weaknesses,
    string? Likes,
    string? Dislikes,
    Guid? PortraitId,
    Guid? PlayerPortraitSubmissionId,
    string Status,
    JsonElement? FormData,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    bool IsEditable,
    long Version)
{
    public static CharacterApplicationResponse From(CharacterApplication a) =>
        new(
            a.Id,
            EnumNaming.ToDbValue(a.Role),
            EnumNaming.ToDbValue(a.Sex),
            Empty(a.FamilyName),
            Empty(a.GivenName),
            a.CourtesyName,
            a.BirthDateLabel,
            a.Age,
            Empty(a.Appearance),
            Empty(a.Biography),
            Empty(a.Personality),
            Empty(a.Strengths),
            Empty(a.Weaknesses),
            Empty(a.Likes),
            Empty(a.Dislikes),
            a.PortraitId,
            a.PlayerPortraitSubmissionId,
            EnumNaming.ToDbValue(a.Status),
            ParseObject(a.FormData),
            a.SubmittedAt,
            a.ReviewedAt,
            // The review note is shown to the player because it is the revision request
            // itself; who wrote it is not.
            a.ReviewNote,
            // Saves the front end from re-deriving the status rules to decide whether to
            // render the form read-only.
            a.Status is ApplicationStatus.Draft or ApplicationStatus.NeedsRevision,
            a.Version);

    /// <summary>An unfilled draft field is null to the client, not an empty string.</summary>
    private static string? Empty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static JsonElement? ParseObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary><c>PortraitSummaryDto</c> — one official illustration in the picker.</summary>
public sealed record PortraitSummaryResponse(
    Guid Id,
    string Code,
    string Role,
    string DisplayName,
    string AssetUrl,
    string? ThumbnailUrl,
    int SortOrder)
{
    public static PortraitSummaryResponse From(PresetPortrait p) =>
        new(p.Id, p.Code, EnumNaming.ToDbValue(p.Role), p.DisplayName, p.AssetUrl, p.ThumbnailUrl, p.SortOrder);
}

/// <summary>
/// <c>PortraitUploadDto</c>. The storage key never appears — the image is reachable only
/// through the controlled media endpoint, which re-checks ownership and review state.
/// </summary>
public sealed record PortraitUploadResponse(
    Guid Id,
    string Role,
    string Status,
    string PreviewUrl,
    decimal CropX,
    decimal CropY,
    decimal CropWidth,
    decimal CropHeight,
    int Width,
    int Height,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    long Version)
{
    public static PortraitUploadResponse From(PlayerPortraitSubmission s) =>
        new(
            s.Id,
            EnumNaming.ToDbValue(s.Role),
            EnumNaming.ToDbValue(s.Status),
            $"/api/v1/media/{s.MediaAssetId}/content?variant=portrait",
            s.CropX,
            s.CropY,
            s.CropWidth,
            s.CropHeight,
            s.MediaAsset?.Width ?? 0,
            s.MediaAsset?.Height ?? 0,
            s.ReviewNote,
            s.CreatedAt,
            s.Version);
}
