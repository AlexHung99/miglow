using GongWei.Domain.Characters;
using GongWei.Domain.Common;

namespace GongWei.Domain.Events;

/// <summary>Table: event_rooms.</summary>
public class EventRoom : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Summary { get; set; } = string.Empty;

    public string BodyMarkdown { get; set; } = string.Empty;

    public EventType EventType { get; set; }

    public EventRoomStatus Status { get; set; } = EventRoomStatus.Draft;

    public Guid? LocationId { get; set; }

    public EventVisibility Visibility { get; set; } = EventVisibility.Public;

    public int? ParticipantLimit { get; set; }

    public string RulesVersion { get; set; } = null!;

    /// <summary>
    /// jsonb — the rules as they stood when the room was created. Later story or setting
    /// edits never rewrite a room that has already started (§6.9).
    /// </summary>
    public string RulesSnapshot { get; set; } = "{}";

    public DateTimeOffset? OpensAt { get; set; }

    public DateTimeOffset? DeadlineAt { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();

    public ICollection<EventPost> Posts { get; set; } = new List<EventPost>();

    public bool AcceptsPostsAt(DateTimeOffset now) =>
        Status == EventRoomStatus.Open
        && (OpensAt is null || OpensAt <= now)
        && (DeadlineAt is null || DeadlineAt > now);

    /// <summary>
    /// The pure part of the join check. The participant count is re-read under the room
    /// lock inside the transaction before this is called (§4.4).
    /// </summary>
    public void EnsureCanJoin(Character character, int currentParticipantCount, DateTimeOffset now)
    {
        character.EnsureCanAct();

        if (Status != EventRoomStatus.Open)
        {
            throw DomainException.Conflict(ErrorCodes.EventNotOpen, $"《{Title}》目前不開放加入。");
        }

        if (DeadlineAt is not null && now >= DeadlineAt)
        {
            throw DomainException.Conflict(ErrorCodes.EventNotOpen, $"《{Title}》已於截止時間關閉。");
        }

        if (ParticipantLimit is not null && currentParticipantCount >= ParticipantLimit)
        {
            throw DomainException.Conflict(ErrorCodes.EventFull, $"《{Title}》名額已滿。");
        }
    }

    public void EnsureCanSettle()
    {
        if (Status == EventRoomStatus.Settled)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"《{Title}》已結算，修改結果需走雙人覆核。");
        }

        if (Status is not (EventRoomStatus.Open or EventRoomStatus.Locked))
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"《{Title}》目前為 {EnumNaming.ToDbValue(Status)}，無法結算。");
        }
    }
}

/// <summary>Table: event_participants.</summary>
public class EventParticipant
{
    public Guid EventRoomId { get; set; }

    public EventRoom? EventRoom { get; set; }

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public string ParticipantRole { get; set; } = "participant";

    public ParticipantStatus Status { get; set; } = ParticipantStatus.Joined;

    public DateTimeOffset? JoinedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Metadata { get; set; } = "{}";

    public bool IsEligible => Status is ParticipantStatus.Joined or ParticipantStatus.Completed;
}

/// <summary>
/// Table: event_posts. Player text is written as a draft, submitted, reviewed, and only
/// published once approved. Other players never see anything else (§6.10).
/// </summary>
public class EventPost : IVersioned, IHasId
{
    public const int MaxBodyLength = 10000;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventRoomId { get; set; }

    public EventRoom? EventRoom { get; set; }

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public string BodyMarkdown { get; set; } = string.Empty;

    public EventPostStatus Status { get; set; } = EventPostStatus.Draft;

    /// <summary>De-duplicates retried submits from the same client.</summary>
    public string? ClientRequestId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public string? ReviewNote { get; set; }

    /// <summary>Set only on approval; this is what puts the post in the public feed.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public Guid? ModeratedBy { get; set; }

    public string? ModerationNote { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsPubliclyVisible => Status == EventPostStatus.Approved && PublishedAt is not null;

    /// <summary>The author may edit only while it is theirs to edit (§6.10 步驟 4).</summary>
    public bool IsAuthorEditable =>
        Status is EventPostStatus.Draft or EventPostStatus.NeedsRevision;

    /// <summary>Withdrawable while a draft, or submitted but not yet claimed by a reviewer.</summary>
    public bool IsWithdrawable =>
        Status is EventPostStatus.Draft or EventPostStatus.Submitted or EventPostStatus.NeedsRevision;
}

/// <summary>Table: event_post_revisions — append-only, permanently retained, never public.</summary>
public class EventPostRevision : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventPostId { get; set; }

    public EventPost? Post { get; set; }

    public int RevisionNo { get; set; }

    public string BodyMarkdown { get; set; } = string.Empty;

    public EventPostRevisionKind RevisionKind { get; set; } = EventPostRevisionKind.DraftSave;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Table: event_results — append-only. A NULL character_id is the single global row for
/// the event, enforced by UNIQUE NULLS NOT DISTINCT.
/// </summary>
public class EventResult : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventRoomId { get; set; }

    public EventRoom? EventRoom { get; set; }

    public Guid? CharacterId { get; set; }

    public string OutcomeCode { get; set; } = null!;

    public string PublicSummary { get; set; } = null!;

    /// <summary>jsonb — only the owning player and admins ever see this.</summary>
    public string PrivatePayload { get; set; } = "{}";

    /// <summary>jsonb — the reward snapshot as granted; never rewritten later.</summary>
    public string RewardsPayload { get; set; } = "{}";

    public string RulesVersion { get; set; } = null!;

    public Guid SettledBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsGlobal => CharacterId is null;
}

/// <summary>Table: external_play_submissions — LINE group and offline write-ups.</summary>
public class ExternalPlaySubmission : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SubmittedByCharacterId { get; set; }

    public Character? SubmittedByCharacter { get; set; }

    public ExternalPlaySourceType SourceType { get; set; } = ExternalPlaySourceType.LineGroup;

    public DateTimeOffset OccurredAt { get; set; }

    public string Summary { get; set; } = null!;

    /// <summary>jsonb array</summary>
    public string EvidenceUrls { get; set; } = "[]";

    /// <summary>jsonb array of character ids</summary>
    public string InvolvedCharacterIds { get; set; } = "[]";

    public ExternalPlayStatus Status { get; set; } = ExternalPlayStatus.Submitted;

    public string? ReviewNote { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsPlayerEditable => Status == ExternalPlayStatus.Submitted;
}
