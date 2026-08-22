using GongWei.Domain.Characters;
using GongWei.Domain.Common;

namespace GongWei.Domain.Reproduction;

/// <summary>
/// Table: reproduction_control — singleton row 1, the serialisation point for every
/// reproduction flow. Lock order is always control(1) → pregnancy → wait pool entry →
/// character (§6.2).
/// </summary>
public class ReproductionControl : IVersioned
{
    public const int SingletonId = 1;

    /// <summary>Default is 100%: approving an audience conceives unless an admin lowers this.</summary>
    public const short DefaultConceptionRatePercent = 100;

    public const short DefaultPregnancyDurationDays = 10;

    public short Id { get; set; } = SingletonId;

    public bool IsOpen { get; set; } = true;

    public string? ClosedReason { get; set; }

    /// <summary>
    /// The conception rate, not the chance an admin approves the invitation. Admins only
    /// submit approved/rejected; the roll happens server-side afterwards (§6.2).
    /// </summary>
    public short ConceptionRatePercent { get; set; } = DefaultConceptionRatePercent;

    public short PregnancyDurationDays { get; set; } = DefaultPregnancyDurationDays;

    /// <summary>
    /// Default <see cref="MiscarriageMode.EventOnly"/>: no daily random miscarriage, only
    /// a published event or status-effect rule with a stated reason can end a pregnancy (§6.3).
    /// </summary>
    public MiscarriageMode MiscarriageMode { get; set; } = MiscarriageMode.EventOnly;

    /// <summary>jsonb</summary>
    public string MiscarriageRules { get; set; } = """{"baseRatePercent":0}""";

    public string RulesVersion { get; set; } = "reproduction-1";

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw DomainException.Conflict(
                ErrorCodes.ReproductionClosed, ClosedReason ?? "生育系統目前暫停。");
        }
    }
}

/// <summary>Table: heir_wait_pool_entries — only princes/princesses may wait to be born.</summary>
public class HeirWaitPoolEntry : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public WaitPoolStatus Status { get; set; } = WaitPoolStatus.Waiting;

    public DateTimeOffset EnteredAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? ResolvedReason { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsWaiting => Status == WaitPoolStatus.Waiting;

    /// <summary>Leaving the pool always records when and why, which the CHECK enforces.</summary>
    public void Resolve(WaitPoolStatus terminalStatus, DateTimeOffset now, string reason)
    {
        if (terminalStatus == WaitPoolStatus.Waiting)
        {
            throw DomainException.Validation("waiting 不是可結束的狀態。");
        }

        Status = terminalStatus;
        ResolvedAt = now;
        ResolvedReason = reason;
    }
}

/// <summary>Table: audience_requests — 侍膳 / 侍寢.</summary>
public class AudienceRequest : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public AudienceType AudienceType { get; set; }

    public AudienceRequestStatus Status { get; set; } = AudienceRequestStatus.Submitted;

    /// <summary>jsonb — the eligibility facts as they stood when the request was filed.</summary>
    public string QualificationSnapshot { get; set; } = "{}";

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? ResultCode { get; set; }

    /// <summary>jsonb — the rate, roll and outcome, kept permanently for audit (§6.2).</summary>
    public string ResultPayload { get; set; } = "{}";

    public string IdempotencyKey { get; set; } = null!;

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsPending => Status is AudienceRequestStatus.Submitted or AudienceRequestStatus.Approved;
}

/// <summary>
/// Table: pregnancies. Creating one reserves an heir slot; the slot must be released the
/// moment the pregnancy stops being ongoing, which a CHECK enforces (§6.3).
/// </summary>
public class Pregnancy : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MotherCharacterId { get; set; }

    public Character? Mother { get; set; }

    /// <summary>Required and unique — every pregnancy traces back to one resolved audience.</summary>
    public Guid AudienceRequestId { get; set; }

    public AudienceRequest? AudienceRequest { get; set; }

    public PregnancyStatus Status { get; set; } = PregnancyStatus.Ongoing;

    public DateTimeOffset ConceivedAt { get; set; }

    /// <summary>Computed by the server as conceivedAt + pregnancyDurationDays; never client-supplied.</summary>
    public DateTimeOffset DueAt { get; set; }

    public short ConceptionRatePercent { get; set; }

    /// <summary>The 1–100 roll that produced this pregnancy, kept permanently.</summary>
    public short ConceptionRoll { get; set; }

    public DateTimeOffset SlotReservedAt { get; set; }

    public DateTimeOffset? SlotReleasedAt { get; set; }

    public string RulesVersion { get; set; } = null!;

    public string RulesSnapshot { get; set; } = "{}";

    public Guid? ResolvedBy { get; set; }

    public string? ResolutionCode { get; set; }

    public string? ResolutionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsOngoing => Status == PregnancyStatus.Ongoing;

    public bool IsDueAt(DateTimeOffset now) => IsOngoing && now >= DueAt;

    public void EnsureOngoing()
    {
        if (!IsOngoing)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"此懷孕已為 {EnumNaming.ToDbValue(Status)}。");
        }
    }

    /// <summary>
    /// Ends the pregnancy and releases the reserved heir slot in the same step, so the
    /// two can never drift apart. Miscarriage additionally requires a code and a reason
    /// of at least five characters, which the database also checks (§6.3).
    /// </summary>
    public void Resolve(
        PregnancyStatus terminalStatus,
        DateTimeOffset now,
        Guid? resolvedBy,
        string? resolutionCode,
        string? resolutionReason)
    {
        if (terminalStatus == PregnancyStatus.Ongoing)
        {
            throw DomainException.Validation("ongoing 不是可結束的狀態。");
        }

        EnsureOngoing();

        if (terminalStatus == PregnancyStatus.Miscarried
            && (string.IsNullOrWhiteSpace(resolutionCode) || (resolutionReason?.Trim().Length ?? 0) < 5))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["privateReason"] = ["流產必須填寫觸發代碼與至少 5 字的理由"]
            });
        }

        Status = terminalStatus;
        SlotReleasedAt = now;
        ResolvedBy = resolvedBy;
        ResolutionCode = resolutionCode;
        ResolutionReason = resolutionReason;
    }
}

/// <summary>Table: births — append-only, carries the proof needed to audit a draw (§6.4).</summary>
public class Birth : IHasId
{
    public const string DefaultAlgorithm = "csprng-uniform-v1";

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PregnancyId { get; set; }

    public Pregnancy? Pregnancy { get; set; }

    public Guid WaitPoolEntryId { get; set; }

    public Guid ChildCharacterId { get; set; }

    public int CandidateCount { get; set; }

    /// <summary>Hex SHA-256 over the UUID-sorted candidate id list.</summary>
    public string CandidateSetHash { get; set; } = null!;

    public string RandomAlgorithm { get; set; } = DefaultAlgorithm;

    public string RandomProofHash { get; set; } = null!;

    public string RulesVersion { get; set; } = null!;

    public Guid? DrawnBy { get; set; }

    public DateTimeOffset BornAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: offspring_links — append-only. Parent is a character XOR an NPC code.</summary>
public class OffspringLink : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ChildCharacterId { get; set; }

    public ParentType ParentType { get; set; }

    public Guid? ParentCharacterId { get; set; }

    public string? ParentNpcCode { get; set; }

    public bool IsPublic { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
