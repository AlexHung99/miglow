using GongWei.Domain.Common;

namespace GongWei.Domain.Characters;

/// <summary>
/// Table: ability_label_definitions — maps an ability value onto the word players see.
/// The acceptance criteria are explicit: 體質 570 must come back as「康健」
/// (README_v1.1 §7). Ranges are data, never a switch statement in code.
///
/// Keyed by (ability_code, min_value); each row covers min_value..max_value inclusive.
/// </summary>
public class AbilityLabelDefinition : IVersioned
{
    public AbilityCode AbilityCode { get; set; }

    public short MinValue { get; set; }

    public short MaxValue { get; set; }

    public string DisplayLabel { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool Covers(short value) => value >= MinValue && value <= MaxValue;
}

/// <summary>
/// Table: character_progress — the counters that promotion thresholds are checked
/// against (rank_catalog §1: 主線次數、活躍度、自戲字數、每週訊息).
///
/// Weekly counters reset by advancing <see cref="WeekStartDate"/> rather than by a
/// scheduled wipe, so a missed job cannot silently zero a player's week.
/// </summary>
public class CharacterProgress : IVersioned
{
    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public long SettledEventCount { get; set; }

    public long ApprovedEventPostCount { get; set; }

    public long ApprovedExternalPlayCount { get; set; }

    public long SelfPlayWordCount { get; set; }

    /// <summary>Monday of the court week the weekly counter belongs to.</summary>
    public DateOnly WeekStartDate { get; set; }

    public int WeeklyMessageCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>
    /// Rolls the weekly counter forward when the week has changed, returning true if a
    /// reset happened. Reading is enough to keep the counter honest — no cron needed.
    /// </summary>
    public bool RollWeekIfNeeded(DateOnly currentWeekStart)
    {
        if (WeekStartDate == currentWeekStart)
        {
            return false;
        }

        WeekStartDate = currentWeekStart;
        WeeklyMessageCount = 0;
        return true;
    }
}

/// <summary>
/// Table: character_chronicle_entries — the unified history a player sees on their own
/// page. Every settlement, adjustment, rank change and reproduction outcome writes one
/// of these in the same transaction as the change itself (README_v1.1 §7).
/// </summary>
public class CharacterChronicleEntry : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public ChronicleEntryType EntryType { get; set; }

    public ChronicleVisibility Visibility { get; set; } = ChronicleVisibility.Public;

    public string Title { get; set; } = null!;

    public string Detail { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    /// <summary>What produced this row, e.g. event_result / ledger_transaction.</summary>
    public string SourceType { get; set; } = null!;

    public Guid? SourceId { get; set; }

    /// <summary>jsonb array — ability deltas, so the page can render 體質 +5 without a join.</summary>
    public string StatChanges { get; set; } = "[]";

    /// <summary>jsonb array — currency and item deltas.</summary>
    public string ResourceChanges { get; set; } = "[]";

    /// <summary>In-world time of the change, which may differ from when the row was written.</summary>
    public DateTimeOffset HappenedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public string? RequestId { get; set; }

    public string Metadata { get; set; } = "{}";

    /// <summary>Other players only ever see public rows (README_v1.1 §7).</summary>
    public bool IsPubliclyVisible => Visibility == ChronicleVisibility.Public;
}
