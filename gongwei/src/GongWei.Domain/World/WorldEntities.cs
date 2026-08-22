using GongWei.Domain.Common;

namespace GongWei.Domain.World;

/// <summary>
/// Table: world_state — singleton row 1. The court calendar runs 1:1 against real time
/// in Asia/Taipei, computed from the two anchor dates rather than from a resident timer,
/// so a worker restart cannot lose or double-count a day (§6.12).
/// </summary>
public class WorldState : IVersioned
{
    public const int SingletonId = 1;

    /// <summary>Only 1:1 real time is supported; the column CHECK allows nothing else.</summary>
    public const string RealtimeCalendarMode = "realtime_1to1";

    public short Id { get; set; } = SingletonId;

    /// <summary>In-world era, e.g. 永熙. v1.1 renamed this from chapter_code — the main-story module is gone.</summary>
    public string EraCode { get; set; } = null!;

    /// <summary>In-world year label, e.g. 永熙七年.</summary>
    public string DisplayYear { get; set; } = null!;

    public Season Season { get; set; }

    /// <summary>In-world day label, e.g. 三月初七.</summary>
    public string DayLabel { get; set; } = null!;

    public string CalendarMode { get; set; } = RealtimeCalendarMode;

    public string CalendarTimezone { get; set; } = "Asia/Taipei";

    public DateOnly CalendarAnchorRealDate { get; set; }

    public DateOnly CalendarAnchorGameDate { get; set; }

    public bool ReproductionOpen { get; set; } = true;

    public bool MaintenanceMode { get; set; }

    /// <summary>jsonb — global switches read by the rules engine.</summary>
    public string Config { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>
    /// The in-world date for a given real date, at 1:1 speed. Pure arithmetic on the
    /// anchors, so it gives the same answer however often it is called.
    /// </summary>
    public DateOnly GameDateFor(DateOnly realDate) =>
        CalendarAnchorGameDate.AddDays(realDate.DayNumber - CalendarAnchorRealDate.DayNumber);
}

/// <summary>
/// Table: game_settings, keyed by <c>setting_key</c>. Allowlist only — the admin site can
/// never edit secrets, connection strings, file paths or arbitrary JSON keys (§6.9).
/// </summary>
public class GameSetting : IVersioned
{
    public string SettingKey { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    /// <summary>jsonb, NOT NULL — a setting always has a live value players can read.</summary>
    public string PublishedValue { get; set; } = null!;

    /// <summary>jsonb — editor scratch space; never visible to players.</summary>
    public string? DraftValue { get; set; }

    /// <summary>jsonb — JSON Schema fragment validated in C# before any write.</summary>
    public string ValidationSchema { get; set; } = "{}";

    public SettingRiskLevel RiskLevel { get; set; } = SettingRiskLevel.Normal;

    /// <summary>Public settings are readable without a session, e.g. the support link.</summary>
    public bool IsPublic { get; set; }

    public Guid UpdatedBy { get; set; }

    public Guid? PublishedBy { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>High-risk settings only take effect through the two-person flow (§9.2).</summary>
    public bool RequiresApprovalToPublish => RiskLevel == SettingRiskLevel.High;

    public bool HasPendingDraft => DraftValue is not null && DraftValue != PublishedValue;
}

/// <summary>Table: game_setting_revisions — append-only publish history.</summary>
public class GameSettingRevision : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string SettingKey { get; set; } = null!;

    public GameSetting? Setting { get; set; }

    public int RevisionNo { get; set; }

    public string? PreviousValue { get; set; }

    public string PublishedValue { get; set; } = null!;

    public string ChangeReason { get; set; } = null!;

    public Guid? ApprovalRequestId { get; set; }

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}

/// <summary>Table: world_locations. With the story module gone, the map is locations + NPCs + events.</summary>
public class WorldLocation : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal MapX { get; set; }

    public decimal MapY { get; set; }

    /// <summary>jsonb — allowlisted visibility/entry rules evaluated server-side.</summary>
    public string AccessRules { get; set; } = "{}";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;
}

/// <summary>
/// Table: npcs — the NPC content CMS introduced in v1.1. Players only ever read
/// published NPCs; drafts and reviews stay admin-only (README_v1.1 §7).
/// </summary>
public class Npc : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public NpcSex? Sex { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string StoryMarkdown { get; set; } = string.Empty;

    /// <summary>jsonb — the structured public profile shown on the NPC page.</summary>
    public string PublicProfile { get; set; } = "{}";

    /// <summary>Either an uploaded asset or a static URL must be present.</summary>
    public Guid? PortraitAssetId { get; set; }

    public string? PortraitUrl { get; set; }

    public Guid? PrimaryLocationId { get; set; }

    public WorldLocation? PrimaryLocation { get; set; }

    public NpcStatus Status { get; set; } = NpcStatus.Draft;

    public int SortOrder { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? PublishedBy { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsPlayerVisible => Status == NpcStatus.Published && PublishedAt is not null;
}

/// <summary>
/// Table: npc_revisions — append-only. A deployment seed may only insert missing NPC
/// codes; existing NPCs are changed through publish/restore, never overwritten
/// (README_v1.1 §5).
/// </summary>
public class NpcRevision : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid NpcId { get; set; }

    public Npc? Npc { get; set; }

    public int RevisionNo { get; set; }

    public string Snapshot { get; set; } = "{}";

    public ContentChangeKind ChangeKind { get; set; }

    public string? ChangeNote { get; set; }

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
