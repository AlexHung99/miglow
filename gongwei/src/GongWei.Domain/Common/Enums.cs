namespace GongWei.Domain.Common;

// Every enum below is persisted as the snake_cased member name and is mirrored by a
// CHECK constraint in db/authoritative/schema_v1.0.sql. Adding a member needs a migration.
// The Postgres integration tests assert both lists stay identical.

// ------------------------------------------------------------------ identity

public enum UserStatus { Active, Suspended, Deleted }

/// <summary>Fixed admin role list (後端規格書 v1.0 §9.1).</summary>
public enum AdminRole
{
    SuperAdmin,
    CharacterReviewer,
    GameMaster,
    EconomyManager,
    Moderator,
    Auditor,
    ContentEditor,
    CharacterManager,
    SystemConfigManager
}

// ---------------------------------------------------------------- characters

public enum CharacterRole { Consort, Prince, Princess }

/// <summary>
/// Never supplied by the client: 規格 §13.1 derives it from the role, so a request can
/// never produce a prince who is female.
/// </summary>
public enum Sex { Female, Male }

public enum CharacterStatus { WaitingBirth, Active, Paused, Dead, Suspended, Archived }

public enum ApplicationStatus { Draft, Submitted, NeedsRevision, Approved, Rejected, Cancelled }

public enum TitleCategory { Rank, Achievement, Story, Honorary, Secret }

public enum TitleVisibility { Public, OwnerOnly, AdminOnly }

// --------------------------------------------------------------------- media

public enum MediaAssetStatus { Uploaded, Processing, Ready, Quarantined, Deleted }

public enum PortraitSubmissionStatus { Pending, Approved, Rejected, Withdrawn }

// --------------------------------------------------------------------- world

public enum Season { Spring, Summer, Autumn, Winter }

public enum SettingRiskLevel { Normal, High }

// ----------------------------------------------------------------------- NPC

/// <summary>
/// NPC content lifecycle. v1.1 dropped the main-story module; the map is now made of
/// locations, NPCs and location events (README_v1.1 §2).
/// </summary>
public enum NpcStatus { Draft, Review, Published, Archived }

/// <summary>NPCs may be of unspecified sex, unlike player characters.</summary>
public enum NpcSex { Female, Male, Unknown }

public enum ContentChangeKind { Create, Edit, Publish, Archive, Restore }

// ------------------------------------------------------------------ character

/// <summary>The four abilities that carry a display label, e.g. 體質 570 → 康健.</summary>
public enum AbilityCode { Vitality, Appearance, Strategy, Luck }

/// <summary>
/// One row in a character's unified chronicle. Everything that changes a stat, a
/// resource or a standing writes one of these in the same transaction (README_v1.1 §7).
/// </summary>
public enum ChronicleEntryType
{
    Event,
    Economy,
    Inventory,
    Rank,
    Status,
    Reproduction,
    Intrigue,
    Admin,
    System
}

public enum ChronicleVisibility { Public, OwnerOnly, AdminOnly }

// -------------------------------------------------------------------- events

public enum EventType { Main, Social, Investigation, Limited, Private, Admin }

public enum EventRoomStatus { Draft, Scheduled, Open, Locked, Settled, Cancelled }

public enum EventVisibility { Public, Invited, Private }

public enum ParticipantStatus { Invited, Joined, Left, Removed, Completed }

/// <summary>
/// Event text is written as a draft, submitted, reviewed and only then published
/// (§6.10). Only <see cref="Approved"/> is ever visible to other players.
/// </summary>
public enum EventPostStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    NeedsRevision,
    Rejected,
    Withdrawn,
    Moderated
}

public enum EventPostRevisionKind { DraftSave, Submit, RevisionRequest, Approval, Moderation }

public enum ExternalPlaySourceType { LineGroup, Other }

public enum ExternalPlayStatus { Submitted, UnderReview, Approved, Rejected, Cancelled }

// ------------------------------------------------------------------- economy

public enum LedgerTransactionType
{
    Stipend,
    Purchase,
    Reward,
    ItemUse,
    AdminGrant,
    AdminCorrection,
    Refund
}

public enum ItemCategory { Clothing, Medicine, Poison, Gift, Quest, Material, Other }

public enum InventoryTransactionType
{
    Purchase,
    Reward,
    Use,
    Expire,
    AdminGrant,
    AdminCorrection,
    Refund
}

// -------------------------------------------------------------- reproduction

/// <summary>
/// 預設 <see cref="EventOnly"/>：系統不做每日隨機流產，只有符合已發布事件或狀態效果
/// 規則並附理由時才能執行 (§6.3).
/// </summary>
public enum MiscarriageMode { Disabled, EventOnly, Threshold, DailyProbability }

public enum WaitPoolStatus { Waiting, Drawn, Withdrawn, Suspended }

public enum AudienceType { Meal, Bedchamber }

public enum AudienceRequestStatus { Submitted, Approved, Rejected, Resolved, Cancelled }

public enum PregnancyStatus { Ongoing, Miscarried, Completed, Cancelled }

public enum ParentType { Mother, Father }

// ------------------------------------------------------------------- intrigue

public enum IntrigueActionType { Poison, Investigate, Countermeasure }

public enum IntrigueStatus { Submitted, Processing, Resolved, Failed, Cancelled }

public enum EffectVisibility { Private, Public, AdminOnly }

// ----------------------------------------------------------------- operations

public enum AnnouncementSeverity { Info, Warning, Critical }

public enum AnnouncementAudience { All, Players, Admins }

public enum ApprovalStatus { Pending, Approved, Rejected, Expired, Executed, Cancelled }

public enum ApprovalDecisionKind { Approve, Reject }

public enum IdempotencyStatus { Processing, Completed, Failed }

public enum JobRunStatus { Running, Succeeded, Failed, Cancelled }

/// <summary>
/// The registered two-person approval handlers (api_v1_v1.0.md §12). Stored as free
/// text in <c>approval_requests.action_type</c>, but only these values dispatch —
/// there is no "run arbitrary payload" path.
/// </summary>
public static class ApprovalActionTypes
{
    public const string CharacterDeath = "character.death";
    public const string GameSettingHighRiskPublish = "game_setting.high_risk_publish";
    public const string AdminGrantSuperAdmin = "admin.grant_super_admin";
    public const string EventResultAmendment = "event_result.amend";
    public const string BirthResultCorrection = "birth_result.correct";
    public const string BulkCharacterRepair = "character.bulk_repair";
    public const string ProductionConfigChange = "config.production_change";
    public const string WorldChapterAdvance = "world.chapter_advance";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        CharacterDeath,
        GameSettingHighRiskPublish,
        AdminGrantSuperAdmin,
        EventResultAmendment,
        BirthResultCorrection,
        BulkCharacterRepair,
        ProductionConfigChange,
        WorldChapterAdvance
    };
}
