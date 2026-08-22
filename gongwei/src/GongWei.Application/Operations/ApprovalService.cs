using System.Data;
using System.Text.Json.Serialization;
using GongWei.Application.Abstractions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Events;
using GongWei.Domain.Intrigue;
using GongWei.Domain.Operations;
using GongWei.Domain.Reproduction;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Operations;

public sealed record CreateApprovalInput(
    string ActionType,
    string TargetType,
    Guid? TargetId,
    string PayloadJson,
    string Reason,
    TimeSpan? Lifetime);

/// <summary>Payload for <c>character.death</c>.</summary>
public sealed record DeathPayload(
    [property: JsonPropertyName("characterId")] Guid CharacterId,
    [property: JsonPropertyName("causeCode")] string CauseCode,
    [property: JsonPropertyName("publicCause")] string PublicCause,
    [property: JsonPropertyName("sourceType")] string? SourceType,
    [property: JsonPropertyName("sourceId")] Guid? SourceId,
    [property: JsonPropertyName("expectedCharacterVersion")] long? ExpectedCharacterVersion);

/// <summary>
/// Two-person review (§9.2). A request freezes its payload, a second admin decides, and
/// only then can the action execute — re-validating the target's version so nobody
/// approves one state and executes against another.
///
/// Note that currency adjustments, item grants and ledger corrections deliberately do
/// NOT come through here: v1.0 removed the amount threshold and the approval requirement
/// for those, replacing it with a mandatory reason and audit row (§6.11, §9.2).
/// </summary>
public sealed class ApprovalService(
    IGongWeiDb db,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOutboxWriter outbox,
    IJsonSerializer json)
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    /// <summary>Which admin roles may raise, review and execute each action type.</summary>
    private static readonly Dictionary<string, AdminRole[]> HandlerRoles = new(StringComparer.Ordinal)
    {
        [ApprovalActionTypes.CharacterDeath] = [AdminRole.GameMaster, AdminRole.Moderator],
        [ApprovalActionTypes.GameSettingHighRiskPublish] = [AdminRole.SystemConfigManager, AdminRole.GameMaster],
        [ApprovalActionTypes.AdminGrantSuperAdmin] = [AdminRole.SuperAdmin],
        [ApprovalActionTypes.EventResultAmendment] = [AdminRole.GameMaster],
        [ApprovalActionTypes.BirthResultCorrection] = [AdminRole.GameMaster],
        [ApprovalActionTypes.BulkCharacterRepair] = [AdminRole.SuperAdmin],
        [ApprovalActionTypes.ProductionConfigChange] = [AdminRole.SuperAdmin],
        [ApprovalActionTypes.WorldChapterAdvance] = [AdminRole.GameMaster]
    };

    public async Task<ApprovalRequest> CreateAsync(CreateApprovalInput input, CancellationToken ct = default)
    {
        var roles = RolesFor(input.ActionType);
        currentUser.RequireRole(roles);

        var requesterId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (!json.IsValidObject(input.PayloadJson))
        {
            throw DomainException.Validation("覆核 payload 必須是 JSON 物件。");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["reason"] = ["雙人覆核案件必須說明理由"]
            });
        }

        var request = new ApprovalRequest
        {
            ActionType = input.ActionType,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            Payload = input.PayloadJson,
            Reason = input.Reason,
            Status = ApprovalStatus.Pending,
            RequestedBy = requesterId,
            RequestedAt = now,
            ExpiresAt = now.Add(input.Lifetime ?? DefaultLifetime)
        };

        db.ApprovalRequests.Add(request);

        audit.Write("approval.create", "approval_request", request.Id,
            after: new { input.ActionType, input.TargetType, input.TargetId },
            reason: input.Reason);
        outbox.Enqueue("approval.created", "approval_request", request.Id,
            new { approvalRequestId = request.Id, input.ActionType, requestedBy = requesterId });

        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<ApprovalRequest> DecideAsync(
        Guid requestId,
        long expectedVersion,
        ApprovalDecisionKind decision,
        string? note,
        CancellationToken ct = default)
    {
        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.LockRowAsync("approval_requests", requestId, ct);

        var request = await LoadAsync(requestId, ct);
        request.EnsureVersion(expectedVersion);

        currentUser.RequireRole(RolesFor(request.ActionType));
        request.EnsureReviewerAllowed(reviewerId);

        if (request.Status != ApprovalStatus.Pending)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"此案件已為 {EnumNaming.ToDbValue(request.Status)}。");
        }

        if (request.IsExpiredAt(now))
        {
            request.Status = ApprovalStatus.Expired;
            request.ResolvedAt = now;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            throw DomainException.Conflict(ErrorCodes.ApprovalExpired, "此覆核案件已逾期。");
        }

        db.ApprovalDecisions.Add(new ApprovalDecision
        {
            ApprovalRequestId = request.Id,
            ReviewerId = reviewerId,
            Decision = decision,
            Note = note,
            DecidedAt = now
        });

        request.Status = decision == ApprovalDecisionKind.Approve
            ? ApprovalStatus.Approved
            : ApprovalStatus.Rejected;
        request.ResolvedAt = now;

        audit.Write("approval.decide", "approval_request", request.Id,
            after: new
            {
                decision = EnumNaming.ToDbValue(decision),
                status = EnumNaming.ToDbValue(request.Status)
            },
            reason: note);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return request;
    }

    public async Task<ApprovalRequest> CancelAsync(
        Guid requestId,
        string reason,
        CancellationToken ct = default)
    {
        var actorId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var request = await LoadAsync(requestId, ct);

        // Only the requester or a super admin may cancel, and only while still pending.
        if (request.RequestedBy != actorId && !currentUser.HasRole(AdminRole.SuperAdmin))
        {
            throw DomainException.Forbidden("只有申請人或 super_admin 可以取消此案件。");
        }

        if (request.Status != ApprovalStatus.Pending)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, "只有待決案件可以取消。");
        }

        request.Status = ApprovalStatus.Cancelled;
        request.ResolvedAt = now;

        audit.Write("approval.cancel", "approval_request", request.Id, reason: reason);
        await db.SaveChangesAsync(ct);

        return request;
    }

    /// <summary>
    /// Executes an approved request. Dispatch is by registered action type only — there is
    /// no arbitrary SQL, type name or free payload path (api_v1_v1.0.md §12).
    /// </summary>
    public async Task<ApprovalRequest> ExecuteAsync(
        Guid requestId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var executorId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.LockRowAsync("approval_requests", requestId, ct);

        var request = await LoadAsync(requestId, ct);
        request.EnsureVersion(expectedVersion);

        currentUser.RequireRole(RolesFor(request.ActionType));
        request.EnsureExecutable(now);

        switch (request.ActionType)
        {
            case ApprovalActionTypes.CharacterDeath:
                await ExecuteCharacterDeathAsync(request, executorId, now, ct);
                break;

            default:
                // Every action type is reachable from the admin UI, but only the ones wired
                // up here can execute. The rest fail loudly rather than silently marking
                // themselves done.
                throw DomainException.Conflict(
                    ErrorCodes.ConflictState,
                    $"Handler「{request.ActionType}」尚未接上執行器。");
        }

        request.Status = ApprovalStatus.Executed;
        request.ExecutedAt = now;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return request;
    }

    /// <summary>
    /// Permanent death (§6.7). Cancels everything the character can no longer take part
    /// in, keeps every historical record, frees the account's current-character slot and
    /// leaves the player's login intact.
    /// </summary>
    private async Task ExecuteCharacterDeathAsync(
        ApprovalRequest request,
        Guid executorId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var payload = json.Deserialize<DeathPayload>(request.Payload)
                      ?? throw DomainException.Validation("死亡 payload 格式錯誤。");

        await db.LockCharactersAsync([payload.CharacterId], ct);

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == payload.CharacterId, ct)
                        ?? throw DomainException.NotFound("Character", payload.CharacterId);

        // Approving one state and executing against another is exactly what this catches.
        if (payload.ExpectedCharacterVersion is { } expected && character.Version != expected)
        {
            throw DomainException.VersionConflict(character.Version);
        }

        CharacterLifecycle.EnsureCanTransition(character.Status, CharacterStatus.Dead);

        // A character whose birth slot is already reserved by a pregnancy cannot simply
        // die — the pregnancy has to be resolved through the same admin flow first (§6.7).
        var reservedByPregnancy = await db.HeirWaitPoolEntries.AnyAsync(
            e => e.CharacterId == character.Id && e.Status == WaitPoolStatus.Drawn, ct);

        if (reservedByPregnancy)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                "此角色已被出生流程抽中，請先處理對應的 Pregnancy 再執行死亡。");
        }

        var previousStatus = character.Status;
        character.Status = CharacterStatus.Dead;
        character.DiedAt = now;

        db.Deaths.Add(new Death
        {
            CharacterId = character.Id,
            CauseCode = payload.CauseCode,
            PublicCause = payload.PublicCause,
            PrivateDetails = request.Payload,
            SourceType = payload.SourceType,
            SourceId = payload.SourceId,
            OccurredAt = now,
            RuledBy = executorId,
            ApprovalRequestId = request.Id,
            CreatedAt = now
        });

        db.CharacterStatusHistories.Add(new CharacterStatusHistory
        {
            CharacterId = character.Id,
            FromStatus = previousStatus,
            ToStatus = CharacterStatus.Dead,
            ReasonCode = payload.CauseCode,
            ReasonText = request.Reason,
            ChangedBy = executorId,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        });

        // --- cancel what the character can no longer continue (§6.7 table) ---

        var participations = await db.EventParticipants
            .Where(p => p.CharacterId == character.Id
                        && (p.Status == ParticipantStatus.Invited || p.Status == ParticipantStatus.Joined))
            .ToListAsync(ct);

        foreach (var participation in participations)
        {
            participation.Status = ParticipantStatus.Removed;
        }

        // Unfinished text becomes Withdrawn but keeps its full body and revisions.
        var openPosts = await db.EventPosts
            .Where(p => p.CharacterId == character.Id
                        && (p.Status == EventPostStatus.Draft
                            || p.Status == EventPostStatus.Submitted
                            || p.Status == EventPostStatus.UnderReview
                            || p.Status == EventPostStatus.NeedsRevision))
            .ToListAsync(ct);

        foreach (var post in openPosts)
        {
            post.Status = EventPostStatus.Withdrawn;
            post.ReviewNote = "character.dead";
        }

        var pendingIntrigue = await db.IntrigueActions
            .Where(a => a.ActorCharacterId == character.Id
                        && (a.Status == IntrigueStatus.Submitted || a.Status == IntrigueStatus.Processing))
            .ToListAsync(ct);

        foreach (var action in pendingIntrigue)
        {
            action.Status = IntrigueStatus.Cancelled;
            action.ResolvedAt = now;
        }

        var pendingAudience = await db.AudienceRequests
            .Where(r => r.CharacterId == character.Id
                        && (r.Status == AudienceRequestStatus.Submitted
                            || r.Status == AudienceRequestStatus.Approved))
            .ToListAsync(ct);

        foreach (var audienceRequest in pendingAudience)
        {
            audienceRequest.Status = AudienceRequestStatus.Cancelled;
            audienceRequest.ResolvedAt = now;
            audienceRequest.ResultCode = "character.dead";
        }

        var externalSubmissions = await db.ExternalPlaySubmissions
            .Where(s => s.SubmittedByCharacterId == character.Id
                        && (s.Status == ExternalPlayStatus.Submitted
                            || s.Status == ExternalPlayStatus.UnderReview))
            .ToListAsync(ct);

        foreach (var submission in externalSubmissions)
        {
            submission.Status = ExternalPlayStatus.Cancelled;
        }

        var activeEffects = await db.StatusEffects
            .Where(e => e.CharacterId == character.Id && e.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var effect in activeEffects)
        {
            effect.ResolvedAt = now;
        }

        // --- reproduction: release both a pregnancy and a wait-pool seat ---
        await db.LockReproductionControlAsync(ct);

        var pregnancy = await db.Pregnancies.FirstOrDefaultAsync(
            p => p.MotherCharacterId == character.Id && p.Status == PregnancyStatus.Ongoing, ct);

        if (pregnancy is not null)
        {
            pregnancy.Resolve(
                PregnancyStatus.Cancelled, now, executorId, "mother.dead", request.Reason);
        }

        var poolEntry = await db.HeirWaitPoolEntries.FirstOrDefaultAsync(
            e => e.CharacterId == character.Id && e.Status == WaitPoolStatus.Waiting, ct);

        poolEntry?.Resolve(WaitPoolStatus.Withdrawn, now, "character.dead");

        db.Notifications.Add(new Notification
        {
            UserId = character.UserId,
            NotificationType = "character.died",
            Title = $"{character.FullName} 已薨逝",
            Body = payload.PublicCause,
            Route = $"/characters/{character.Id}",
            CreatedAt = now
        });

        audit.Write("character.death", "character", character.Id,
            before: new { status = EnumNaming.ToDbValue(previousStatus) },
            after: new
            {
                status = EnumNaming.ToDbValue(CharacterStatus.Dead),
                causeCode = payload.CauseCode,
                removedParticipations = participations.Count,
                withdrawnPosts = openPosts.Count,
                cancelledIntrigue = pendingIntrigue.Count,
                pregnancyCancelled = pregnancy is not null,
                waitPoolWithdrawn = poolEntry is not null
            },
            reason: request.Reason);

        outbox.Enqueue("character.died", "character", character.Id, new
        {
            characterId = character.Id,
            character.UserId,
            causeCode = payload.CauseCode
        });
    }

    private static AdminRole[] RolesFor(string actionType)
    {
        if (!ApprovalActionTypes.All.Contains(actionType))
        {
            throw DomainException.Validation($"未註冊的覆核類型「{actionType}」。");
        }

        return HandlerRoles.TryGetValue(actionType, out var roles) ? roles : [AdminRole.SuperAdmin];
    }

    private async Task<ApprovalRequest> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw DomainException.NotFound("Approval request", id);
}
