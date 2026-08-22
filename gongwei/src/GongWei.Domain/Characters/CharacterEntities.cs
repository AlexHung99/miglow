using GongWei.Domain.Common;
using GongWei.Domain.Identity;

namespace GongWei.Domain.Characters;

/// <summary>
/// Table: ranks. One grade holds several 位號 — 正一品 alone has 聖／御／尊／榮貴妃 —
/// so the natural key is (role, display_name), not (role, ordinal) (rank_catalog_v1.0 §3).
/// </summary>
public class Rank : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public CharacterRole AppliesToRole { get; set; }

    /// <summary>品級, e.g. 正一品. Shared by every 位號 at that grade.</summary>
    public string GradeCode { get; set; } = null!;

    /// <summary>1 = 皇超品 (highest) … 29 = 從九品 (lowest).</summary>
    public int Ordinal { get; set; }

    public long PrestigeRequired { get; set; }

    /// <summary>Paid on the first day of each court month (§6.12).</summary>
    public long MonthlyStipend { get; set; }

    /// <summary>The original 年俸 this rank was derived from; kept so the 12th month can settle the remainder.</summary>
    public long SourceAnnualStipend { get; set; }

    /// <summary>NULL means unlimited; a number means 限一／限二／各一 and is checked under a transaction lock.</summary>
    public int? Capacity { get; set; }

    /// <summary>The ＊ marker in the source documents — 為尊, which does not imply a capacity limit.</summary>
    public bool IsLead { get; set; }

    /// <summary>Only these ranks may be chosen when approving a build-a-character form.</summary>
    public bool IsApplicationOption { get; set; }

    /// <summary>jsonb — the four abilities granted at creation. The client can never supply these.</summary>
    public string? InitialStats { get; set; }

    /// <summary>jsonb — ability/prestige/activity thresholds for promotion.</summary>
    public string PromotionRules { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>
    /// The 12th court month settles the annual remainder so the yearly total matches the
    /// source documents exactly (rank_catalog_v1.0 §2).
    /// </summary>
    public long StipendForCourtMonth(int courtMonth) =>
        courtMonth == 12
            ? SourceAnnualStipend - MonthlyStipend * 11
            : MonthlyStipend;
}

/// <summary>Table: character_title_definitions.</summary>
public class CharacterTitleDefinition : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public TitleCategory Category { get; set; }

    /// <summary>Null means the title can be granted to any role.</summary>
    public CharacterRole? AppliesToRole { get; set; }

    public TitleVisibility Visibility { get; set; } = TitleVisibility.Public;

    public string? StyleToken { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;
}

/// <summary>Table: residences.</summary>
public class Residence : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public decimal? MapX { get; set; }

    public decimal? MapY { get; set; }

    /// <summary>NULL means unlimited.</summary>
    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>
    /// The pure part of the move-in rule. Occupancy is counted under the transaction's
    /// locks before this is called (§4.4).
    /// </summary>
    public void EnsureHasRoom(int currentOccupancy)
    {
        if (!IsActive)
        {
            throw DomainException.Conflict(ErrorCodes.ConflictState, $"{DisplayName} 目前不開放入住。");
        }

        if (Capacity is not null && currentOccupancy >= Capacity)
        {
            throw DomainException.Conflict(ErrorCodes.ConflictState, $"{DisplayName} 已達容納上限。");
        }
    }
}

/// <summary>
/// Table: character_applications. Draft may be incomplete and saved repeatedly; the full
/// field rules only apply from Submit onwards (§0.2, §13.1).
/// </summary>
public class CharacterApplication : IVersioned, IHasId
{
    /// <summary>Heirs all carry the imperial surname.</summary>
    public const string ImperialFamilyName = "蕭";

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public CharacterRole Role { get; set; }

    /// <summary>Derived from <see cref="Role"/>, never accepted from the client (§13.1).</summary>
    public Sex Sex { get; set; }

    public string FamilyName { get; set; } = string.Empty;

    public string GivenName { get; set; } = string.Empty;

    public string? CourtesyName { get; set; }

    /// <summary>In-world birthday text. Heirs may leave this null until the birth transaction writes it.</summary>
    public string? BirthDateLabel { get; set; }

    public short? Age { get; set; }

    public string Appearance { get; set; } = string.Empty;

    public string Biography { get; set; } = string.Empty;

    public string Personality { get; set; } = string.Empty;

    public string Strengths { get; set; } = string.Empty;

    public string Weaknesses { get; set; } = string.Empty;

    public string Likes { get; set; } = string.Empty;

    public string Dislikes { get; set; } = string.Empty;

    public Guid? PortraitId { get; set; }

    public PresetPortrait? Portrait { get; set; }

    public Guid? PlayerPortraitSubmissionId { get; set; }

    public PlayerPortraitSubmission? PlayerPortraitSubmission { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    /// <summary>jsonb — extra questionnaire answers that are not first-class columns.</summary>
    public string FormData { get; set; } = "{}";

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public string? ReviewNote { get; set; }

    public Guid? CreatedCharacterId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public ICollection<CharacterApplicationRevision> Revisions { get; set; } =
        new List<CharacterApplicationRevision>();

    public bool IsOpen => ApplicationLifecycle.IsOpen(Status);

    /// <summary>Only the player may cancel, and only before a reviewer claims it (§5.1).</summary>
    public bool CanBeCancelledByPlayer =>
        Status is ApplicationStatus.Draft or ApplicationStatus.NeedsRevision or ApplicationStatus.Submitted;

    public static Sex SexFor(CharacterRole role) =>
        role == CharacterRole.Prince ? Sex.Male : Sex.Female;

    /// <summary>
    /// Full submit-time validation (§13.1). Returns every failing field at once so the
    /// player fixes the whole form in one pass rather than one message at a time.
    /// </summary>
    public void EnsureReadyForSubmission()
    {
        var errors = new Dictionary<string, string[]>();

        void Require(string field, string value, int minimum, string label)
        {
            if (value.Trim().Length < minimum)
            {
                errors[field] = [$"{label}至少需要 {minimum} 字"];
            }
        }

        if (GivenName.Trim().Length is < 1 or > 30)
        {
            errors[nameof(GivenName)] = ["名字需為 1–30 字"];
        }

        Require(nameof(Appearance), Appearance, 60, "容貌");
        Require(nameof(Personality), Personality, 50, "性格");
        Require(nameof(Strengths), Strengths, 50, "擅長");
        Require(nameof(Weaknesses), Weaknesses, 50, "不擅長");
        Require(nameof(Likes), Likes, 50, "喜好");
        Require(nameof(Dislikes), Dislikes, 50, "厭惡");
        Require(nameof(Biography), Biography, 200, "自介");

        if (Role == CharacterRole.Consort)
        {
            if (Age is not (>= 15 and <= 18))
            {
                errors[nameof(Age)] = ["宮妃年齡需為 15–18 歲"];
            }

            if (FamilyName.Trim().Length == 0)
            {
                errors[nameof(FamilyName)] = ["宮妃必須填寫姓氏"];
            }
        }
        else
        {
            if (Age != 0)
            {
                errors[nameof(Age)] = ["皇嗣年齡固定為 0"];
            }

            if (FamilyName != ImperialFamilyName)
            {
                errors[nameof(FamilyName)] = [$"皇嗣姓氏固定為「{ImperialFamilyName}」"];
            }
        }

        var portraitSources = (PortraitId is not null ? 1 : 0)
                            + (PlayerPortraitSubmissionId is not null ? 1 : 0);

        if (portraitSources != 1)
        {
            errors["portrait"] = ["必須且只能選擇一張官方立繪或一張已上傳的人物圖片"];
        }

        if (errors.Count > 0)
        {
            throw DomainException.FieldErrors(errors);
        }
    }
}

/// <summary>Table: character_application_revisions — append-only.</summary>
public class CharacterApplicationRevision : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ApplicationId { get; set; }

    public CharacterApplication? Application { get; set; }

    public int RevisionNo { get; set; }

    public string Snapshot { get; set; } = "{}";

    public Guid ChangedBy { get; set; }

    public string? ChangeReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: characters.</summary>
public class Character : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Required and unique: every character traces back to exactly one approved form.</summary>
    public Guid SourceApplicationId { get; set; }

    public CharacterRole Role { get; set; }

    public Sex Sex { get; set; }

    public string? FamilyName { get; set; }

    public string GivenName { get; set; } = null!;

    public string? CourtesyName { get; set; }

    public string? BirthDateLabel { get; set; }

    public short AgeAtCreation { get; set; }

    public string Appearance { get; set; } = null!;

    public string Biography { get; set; } = string.Empty;

    public string Personality { get; set; } = string.Empty;

    public string Strengths { get; set; } = null!;

    public string Weaknesses { get; set; } = null!;

    public string Likes { get; set; } = null!;

    public string Dislikes { get; set; } = null!;

    public Guid? PortraitId { get; set; }

    public PresetPortrait? Portrait { get; set; }

    public Guid? PlayerPortraitSubmissionId { get; set; }

    public PlayerPortraitSubmission? PlayerPortraitSubmission { get; set; }

    public Guid? RankId { get; set; }

    public Rank? Rank { get; set; }

    public Guid? ResidenceId { get; set; }

    public Residence? Residence { get; set; }

    public CharacterStatus Status { get; set; }

    public string? PauseReason { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? DiedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public CharacterStats? Stats { get; set; }

    public ICollection<CharacterTitleAssignment> Titles { get; set; } = new List<CharacterTitleAssignment>();

    public string FullName => string.IsNullOrEmpty(FamilyName) ? GivenName : FamilyName + GivenName;

    public bool CanAct => Status == CharacterStatus.Active;

    public bool OccupiesCurrentSlot => CharacterLifecycle.OccupiesCurrentSlot(Status);

    /// <summary>
    /// Guard for any player-initiated action. Everything that spends money, joins an
    /// event or requests an audience goes through here first.
    /// </summary>
    public void EnsureCanAct()
    {
        if (!CanAct)
        {
            throw DomainException.CharacterState($"角色目前狀態為 {EnumNaming.ToDbValue(Status)}，無法執行此操作。");
        }
    }
}

/// <summary>Table: character_title_assignments.</summary>
public class CharacterTitleAssignment : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public Guid TitleDefinitionId { get; set; }

    public CharacterTitleDefinition? TitleDefinition { get; set; }

    public bool IsPrimary { get; set; }

    public Guid GrantedBy { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public string GrantReason { get; set; } = null!;

    public Guid? RevokedBy { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokeReason { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsActive => RevokedAt is null;
}

/// <summary>
/// Table: character_stats. Four abilities on a 0–1000 scale, plus prestige (威望) which
/// drives promotion and favor (恩寵). There is deliberately no action-point column —
/// daily actions are unlimited (§0.2, §6.12).
/// </summary>
public class CharacterStats : IVersioned
{
    public const short MinAbility = 0;
    public const short MaxAbility = 1000;

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    /// <summary>體質</summary>
    public short Vitality { get; set; }

    /// <summary>容貌</summary>
    public short Appearance { get; set; }

    /// <summary>心計</summary>
    public short Strategy { get; set; }

    /// <summary>福氣</summary>
    public short Luck { get; set; }

    /// <summary>威望 — starts at 0 and never goes negative.</summary>
    public long Prestige { get; set; }

    /// <summary>恩寵</summary>
    public int Favor { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public void EnsureInRange()
    {
        var errors = new Dictionary<string, string[]>();

        void Check(string field, short value, string label)
        {
            if (value is < MinAbility or > MaxAbility)
            {
                errors[field] = [$"{label}必須介於 {MinAbility}–{MaxAbility}"];
            }
        }

        Check(nameof(Vitality), Vitality, "體質");
        Check(nameof(Appearance), Appearance, "容貌");
        Check(nameof(Strategy), Strategy, "心計");
        Check(nameof(Luck), Luck, "福氣");

        if (Prestige < 0)
        {
            errors[nameof(Prestige)] = ["威望不可為負"];
        }

        if (Favor is < -1000 or > 1000)
        {
            errors[nameof(Favor)] = ["恩寵必須介於 -1000–1000"];
        }

        if (errors.Count > 0)
        {
            throw DomainException.FieldErrors(errors);
        }
    }

    public static short Clamp(int value) =>
        (short)Math.Clamp(value, MinAbility, MaxAbility);
}

/// <summary>Table: character_status_history — append-only.</summary>
public class CharacterStatusHistory : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public CharacterStatus? FromStatus { get; set; }

    public CharacterStatus ToStatus { get; set; }

    public string ReasonCode { get; set; } = null!;

    public string? ReasonText { get; set; }

    public Guid? ChangedBy { get; set; }

    public string? RequestId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: rank_history — append-only.</summary>
public class RankHistory : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Guid? FromRankId { get; set; }

    public Guid ToRankId { get; set; }

    public string ReasonCode { get; set; } = null!;

    public string? ReasonText { get; set; }

    public Guid? ChangedBy { get; set; }

    /// <summary>Stipend uses the rank in force at payment time; it is never pro-rated (§6.12).</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: character_residence_history.</summary>
public class CharacterResidenceHistory : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Guid ResidenceId { get; set; }

    public DateTimeOffset MovedInAt { get; set; }

    public DateTimeOffset? MovedOutAt { get; set; }

    public string? Reason { get; set; }

    public Guid? ChangedBy { get; set; }

    public bool IsCurrent => MovedOutAt is null;
}
