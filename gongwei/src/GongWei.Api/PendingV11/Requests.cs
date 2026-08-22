using System.ComponentModel.DataAnnotations;
using GongWei.Application.Events;
using GongWei.Domain.Common;

namespace GongWei.Api.Contracts;

public sealed record LineCallbackRequest(
    [property: Required] string Code,
    [property: Required] string State);

public sealed record CreateApplicationRequest(
    [property: Required] CharacterRole RequestedRole,
    [property: Required, StringLength(32, MinimumLength = 1)] string CharacterName,
    [property: StringLength(32)] string? FamilyName,
    [property: StringLength(4000)] string? Biography,
    [property: StringLength(2000)] string? Appearance,
    [property: StringLength(2000)] string? Personality,
    Guid? PresetPortraitId,
    Guid? PlayerPortraitSubmissionId,
    string? AnswersJson);

public sealed record ApproveApplicationRequest(
    Guid? RankId,
    Guid? ResidenceId,
    [property: Range(0, 100)] int Charm,
    [property: Range(0, 100)] int Intellect,
    [property: Range(0, 100)] int Artistry,
    [property: Range(0, 100)] int Stamina,
    [property: StringLength(1000)] string? Note);

public sealed record ReviewNoteRequest(
    [property: Required, StringLength(1000, MinimumLength = 1)] string Note);

public sealed record CropRequest(
    [property: Range(0, 1)] decimal X,
    [property: Range(0, 1)] decimal Y,
    [property: Range(0.01, 1)] decimal Width,
    [property: Range(0.01, 1)] decimal Height);

public sealed record ReviewPortraitRequest(
    bool Approve,
    [property: StringLength(1000)] string? Note);

public sealed record CreateEventPostRequest(
    [property: Required] Guid CharacterId,
    [property: Required, StringLength(8000, MinimumLength = 1)] string Body,
    /// <summary>Client-generated id so a retried submit does not create a second post.</summary>
    [property: Required, StringLength(64, MinimumLength = 8)] string ClientRequestId);

public sealed record EditEventPostRequest(
    [property: Required, StringLength(8000, MinimumLength = 1)] string Body);

public sealed record JoinEventRequest([property: Required] Guid CharacterId);

/// <summary>Note there is no price field — the server computes it (spec §6.5).</summary>
public sealed record PurchaseRequest(
    [property: Required] Guid CharacterId,
    [property: Required] Guid OfferId,
    [property: Range(1, 999)] int Quantity);

public sealed record UseItemRequest(
    [property: Required] Guid CharacterId,
    [property: Range(1, 999)] int Quantity = 1);

public sealed record AudienceRequestBody(
    [property: Required] Guid CharacterId,
    [property: Required] AudienceKind Kind);

public sealed record ResolveAudienceRequest(
    bool? ForceOutcome,
    [property: StringLength(1000)] string? Note);

public sealed record MiscarryRequest(
    [property: Required, StringLength(1000, MinimumLength = 1)] string Reason);

public sealed record SettlementRequest(
    [property: StringLength(20000)] string? GlobalNarrative,
    [property: Required] IReadOnlyList<CharacterSettlement> Characters);

public sealed record CreateApprovalRequestBody(
    [property: Required] ApprovalHandler Handler,
    [property: Required, StringLength(2000, MinimumLength = 1)] string Reason,
    [property: Required] string PayloadJson,
    string? TargetType,
    Guid? TargetId,
    long? TargetVersion);

public sealed record ApprovalDecisionRequest(
    [property: Required] ApprovalDecisionKind Decision,
    [property: StringLength(2000)] string? Note);
