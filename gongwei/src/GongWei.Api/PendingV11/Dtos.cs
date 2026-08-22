using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Events;
using GongWei.Domain.Identity;
using GongWei.Domain.Reproduction;
using GongWei.Domain.World;

namespace GongWei.Api.Contracts;

// API DTOs never expose EF entities directly, and private/admin fields live on separate
// shapes from the public ones (spec §7.2).

public sealed record MeResponse(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    IReadOnlyList<string> AdminRoles,
    CharacterSummaryResponse? CurrentCharacter);

public sealed record CharacterSummaryResponse(
    Guid Id,
    string DisplayName,
    string? FamilyName,
    string Role,
    string Status,
    string? RankCode,
    string? RankName,
    string? ResidenceName,
    string? PortraitUrl,
    string? PrimaryTitle,
    long Version)
{
    public static CharacterSummaryResponse From(Character c, string? portraitUrl, string? primaryTitle) =>
        new(
            c.Id,
            c.DisplayName,
            c.FamilyName,
            EnumNaming.ToDbValue(c.Role),
            EnumNaming.ToDbValue(c.Status),
            c.Rank?.Code,
            c.Rank?.DisplayName,
            c.Residence?.DisplayName,
            portraitUrl,
            primaryTitle,
            c.Version);
}

/// <summary>The character's own view — includes stats and resources the public view hides.</summary>
public sealed record CharacterPrivateResponse(
    CharacterSummaryResponse Summary,
    string? Biography,
    string? Appearance,
    string? Personality,
    CharacterStatsResponse? Stats,
    IReadOnlyList<WalletResponse> Wallets);

public sealed record CharacterStatsResponse(
    int Charm,
    int Intellect,
    int Artistry,
    int Stamina,
    int Favor,
    int Reputation,
    int ActionPoints,
    int ActionPointsMax);

public sealed record WalletResponse(string CurrencyCode, string CurrencyName, long Balance);

public sealed record PresetPortraitResponse(
    Guid Id,
    string Code,
    string DisplayName,
    string AppliesToRole,
    string ImageUrl,
    string? ThumbnailUrl);

public sealed record PortraitSubmissionResponse(
    Guid Id,
    string AppliesToRole,
    decimal CropX,
    decimal CropY,
    decimal CropWidth,
    decimal CropHeight,
    string ReviewStatus,
    string? ReviewNote,
    DateTimeOffset SubmittedAt,
    string ImageUrl,
    long Version)
{
    public static PortraitSubmissionResponse From(PlayerPortraitSubmission s) =>
        new(
            s.Id,
            EnumNaming.ToDbValue(s.AppliesToRole),
            s.CropX, s.CropY, s.CropWidth, s.CropHeight,
            EnumNaming.ToDbValue(s.ReviewStatus),
            s.ReviewNote,
            s.SubmittedAt,
            $"/api/v1/media/portraits/{s.Id}",
            s.Version);
}

public sealed record ApplicationResponse(
    Guid Id,
    string Status,
    string RequestedRole,
    string CharacterName,
    string? FamilyName,
    string? Biography,
    string? Appearance,
    string? Personality,
    Guid? PresetPortraitId,
    Guid? PlayerPortraitSubmissionId,
    string? DecisionNote,
    DateTimeOffset? SubmittedAt,
    Guid? CreatedCharacterId,
    long Version)
{
    public static ApplicationResponse From(CharacterApplication a) =>
        new(
            a.Id,
            EnumNaming.ToDbValue(a.Status),
            EnumNaming.ToDbValue(a.RequestedRole),
            a.CharacterName,
            a.FamilyName,
            a.Biography,
            a.Appearance,
            a.Personality,
            a.PresetPortraitId,
            a.PlayerPortraitSubmissionId,
            a.DecisionNote,
            a.SubmittedAt,
            a.CreatedCharacterId,
            a.Version);
}

/// <summary>Reviewer-facing shape: adds the queue fields players never see.</summary>
public sealed record ApplicationReviewResponse(
    ApplicationResponse Application,
    Guid UserId,
    string PlayerDisplayName,
    Guid? ClaimedBy,
    DateTimeOffset? ClaimedAt,
    string? PortraitReviewStatus);

public sealed record WorldStateResponse(
    int ChapterNo,
    string? ChapterTitle,
    int CourtYear,
    int CourtMonth,
    int CourtDay,
    string Season);

public sealed record MapLocationResponse(
    Guid Id,
    string Code,
    string DisplayName,
    string Kind,
    decimal MapX,
    decimal MapY,
    string? Description,
    string? IconKey);

public sealed record EventSummaryResponse(
    Guid Id,
    string Code,
    string Title,
    string? Summary,
    string Status,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    int ParticipantCount,
    int? MaxParticipants,
    bool HasJoined,
    long Version);

public sealed record EventDetailResponse(
    EventSummaryResponse Summary,
    string? Body,
    string? LocationName,
    int? MaxPostsPerCharacter,
    string Visibility);

public sealed record EventPostResponse(
    Guid Id,
    Guid CharacterId,
    string CharacterName,
    string Body,
    int RevisionCount,
    DateTimeOffset CreatedAt,
    long Version)
{
    public static EventPostResponse From(EventPost p, string characterName) =>
        new(p.Id, p.CharacterId, characterName, p.Body, p.RevisionCount, p.CreatedAt, p.Version);
}

public sealed record MarketOfferResponse(
    Guid Id,
    string Code,
    Guid ItemDefinitionId,
    string ItemName,
    string? ItemDescription,
    string CurrencyCode,
    long UnitPrice,
    int? StockRemaining,
    int? PerCharacterLimit,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt);

public sealed record InventoryItemResponse(
    Guid InventoryEntryId,
    Guid ItemDefinitionId,
    string ItemName,
    string Category,
    int Quantity,
    bool IsConsumable,
    DateTimeOffset? ExpiresAt);

public sealed record LedgerEntryResponse(
    Guid Id,
    string CurrencyCode,
    long Amount,
    long BalanceAfter,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record ReproductionStatusResponse(
    bool IsOpen,
    string? HoldReason,
    int WaitingCount,
    int OngoingPregnancyCount,
    int AvailableSlots);

public sealed record PregnancyResponse(
    Guid Id,
    Guid MotherCharacterId,
    string Status,
    DateTimeOffset ConceivedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ResolvedAt,
    long Version)
{
    public static PregnancyResponse From(Pregnancy p) =>
        new(
            p.Id,
            p.MotherCharacterId,
            EnumNaming.ToDbValue(p.Status),
            p.ConceivedAt,
            p.DueAt,
            p.ResolvedAt,
            p.Version);
}

public sealed record NotificationResponse(
    Guid Id,
    string Kind,
    string Title,
    string? Body,
    string? LinkPath,
    bool IsUnread,
    DateTimeOffset CreatedAt);

public sealed record AnnouncementResponse(
    Guid Id,
    string Title,
    string Body,
    string Severity,
    bool IsPinned,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt);

public sealed record StoryChapterResponse(
    Guid Id,
    string Code,
    int ChapterNo,
    string Title,
    string? Summary,
    Guid? EntryNodeId);

public sealed record StoryNodeResponse(
    Guid Id,
    string Code,
    string NodeType,
    string? Title,
    string? Body,
    bool IsEntry,
    Guid? EventRoomId,
    string OptionsJson);

/// <summary>Cursor paging. Total counts are deliberately not returned (spec §7.2).</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
