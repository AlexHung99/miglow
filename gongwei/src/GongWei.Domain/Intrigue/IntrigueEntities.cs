using GongWei.Domain.Characters;
using GongWei.Domain.Common;

namespace GongWei.Domain.Intrigue;

/// <summary>Table: intrigue_actions — poisoning, investigation and countermeasures.</summary>
public class IntrigueAction : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ActorCharacterId { get; set; }

    public Character? Actor { get; set; }

    public Guid TargetCharacterId { get; set; }

    public Character? Target { get; set; }

    public IntrigueActionType ActionType { get; set; }

    public IntrigueStatus Status { get; set; } = IntrigueStatus.Submitted;

    /// <summary>jsonb — what the player submitted.</summary>
    public string InputPayload { get; set; } = "{}";

    /// <summary>jsonb — never returned to the actor or the target.</summary>
    public string SecretResult { get; set; } = "{}";

    /// <summary>jsonb — the part the participants are allowed to learn.</summary>
    public string PublicResult { get; set; } = "{}";

    public string RulesVersion { get; set; } = null!;

    public string IdempotencyKey { get; set; } = null!;

    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>Secret actions resolve on a delay so the target cannot infer them from timing.</summary>
    public DateTimeOffset? ResolveAfter { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsPending => Status is IntrigueStatus.Submitted or IntrigueStatus.Processing;
}

/// <summary>Table: status_effects — append-only in practice; poisoning, illness, confinement.</summary>
public class StatusEffect : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    /// <summary>Content-driven code, e.g. severe_poison — not a fixed C# enum.</summary>
    public string EffectCode { get; set; } = null!;

    public EffectVisibility Visibility { get; set; } = EffectVisibility.Private;

    public short Severity { get; set; } = 1;

    public string Payload { get; set; } = "{}";

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsActiveAt(DateTimeOffset now) =>
        ResolvedAt is null && StartsAt <= now && (ExpiresAt is null || ExpiresAt > now);
}

/// <summary>
/// Table: deaths — append-only, permanently retained. Death is never decided by an
/// automated rule; it is always executed off an approved two-person request (§1.3, §6.7).
/// </summary>
public class Death : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public string CauseCode { get; set; } = null!;

    /// <summary>What other players are told.</summary>
    public string PublicCause { get; set; } = null!;

    /// <summary>jsonb — the real circumstances, admin-only.</summary>
    public string PrivateDetails { get; set; } = "{}";

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? RuledBy { get; set; }

    public Guid? ApprovalRequestId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
