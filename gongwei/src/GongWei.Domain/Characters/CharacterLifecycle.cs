using GongWei.Domain.Common;

namespace GongWei.Domain.Characters;

/// <summary>The character application state machine (後端規格書 v1.0 §5.1).</summary>
public static class ApplicationLifecycle
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Draft] = [ApplicationStatus.Submitted, ApplicationStatus.Cancelled],
        [ApplicationStatus.Submitted] =
        [
            ApplicationStatus.NeedsRevision,
            ApplicationStatus.Approved,
            ApplicationStatus.Rejected,
            ApplicationStatus.Cancelled
        ],
        [ApplicationStatus.NeedsRevision] = [ApplicationStatus.Submitted, ApplicationStatus.Cancelled],
        [ApplicationStatus.Approved] = [],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Cancelled] = []
    };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Statuses that occupy the "one open application per account" slot.</summary>
    public static bool IsOpen(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.NeedsRevision;

    public static void EnsureCanTransition(ApplicationStatus from, ApplicationStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"申請無法由 {EnumNaming.ToDbValue(from)} 轉為 {EnumNaming.ToDbValue(to)}。");
        }
    }
}

/// <summary>
/// The character state machine (§5.2). Death is terminal, and it immediately frees the
/// account's current-character slot so the player can file a new form (§6.7).
/// </summary>
public static class CharacterLifecycle
{
    private static readonly Dictionary<CharacterStatus, CharacterStatus[]> Allowed = new()
    {
        [CharacterStatus.WaitingBirth] =
        [
            CharacterStatus.Active,
            CharacterStatus.Suspended,
            CharacterStatus.Archived
        ],
        [CharacterStatus.Active] =
        [
            CharacterStatus.Paused,
            CharacterStatus.Suspended,
            CharacterStatus.Dead
        ],
        // Taking leave grants no protection: a paused character can still die (§5.2).
        [CharacterStatus.Paused] =
        [
            CharacterStatus.Active,
            CharacterStatus.Suspended,
            CharacterStatus.Dead
        ],
        [CharacterStatus.Suspended] =
        [
            CharacterStatus.WaitingBirth,
            CharacterStatus.Active,
            CharacterStatus.Paused,
            CharacterStatus.Dead,
            CharacterStatus.Archived
        ],
        [CharacterStatus.Dead] = [CharacterStatus.Archived],
        [CharacterStatus.Archived] = []
    };

    public static bool CanTransition(CharacterStatus from, CharacterStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Statuses that count as "the account's current character". Dead and archived
    /// history never blocks the same LINE account from applying again (§5.2, §6.7).
    /// </summary>
    public static bool OccupiesCurrentSlot(CharacterStatus status) =>
        status is CharacterStatus.WaitingBirth
               or CharacterStatus.Active
               or CharacterStatus.Paused
               or CharacterStatus.Suspended;

    public static CharacterStatus InitialStatusFor(CharacterRole role) =>
        role is CharacterRole.Prince or CharacterRole.Princess
            ? CharacterStatus.WaitingBirth
            : CharacterStatus.Active;

    public static void EnsureCanTransition(CharacterStatus from, CharacterStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"角色無法由 {EnumNaming.ToDbValue(from)} 轉為 {EnumNaming.ToDbValue(to)}。");
        }
    }
}
