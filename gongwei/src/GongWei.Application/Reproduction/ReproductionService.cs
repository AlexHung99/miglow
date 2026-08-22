using System.Data;
using System.Security.Cryptography;
using System.Text;
using GongWei.Application.Abstractions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Reproduction;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Reproduction;

/// <summary>What the player and the admin overview see. All three numbers share one snapshot.</summary>
public sealed record ReproductionStatus(
    bool IsOpen,
    string? ClosedReason,
    int Waiting,
    int Reserved,
    int Available);

/// <summary>The publicly explainable part of the reproduction rules (GET /reproduction/rules).</summary>
public sealed record PublicReproductionRules(
    short ConceptionRatePercent,
    short PregnancyDurationDays,
    string MiscarriageMode,
    string RulesVersion);

public sealed record ResolveAudienceResult(
    AudienceRequest Request,
    Pregnancy? Pregnancy,
    short? Rate,
    short? Roll);

public sealed record BirthDrawResult(
    Guid BirthId,
    Guid ChildCharacterId,
    int CandidateCount,
    string CandidateSetHash);

public sealed record MiscarryInput(
    Guid PregnancyId,
    long ExpectedVersion,
    string TriggerCode,
    string SourceType,
    Guid SourceId,
    string? PublicNote,
    string PrivateReason);

/// <summary>
/// 侍膳／侍寢、待生池、懷孕、出生. Every method that can change the number of waiting
/// heirs or ongoing pregnancies takes the same locks in the same order:
/// reproduction_control(1) → pregnancy → wait pool entry → character (§6.2).
/// </summary>
public sealed class ReproductionService(
    IGongWeiDb db,
    IClock clock,
    IRandomProvider random,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOutboxWriter outbox,
    IJsonSerializer json)
{
    public async Task<ReproductionStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var control = await LoadControlAsync(ct);
        var (waiting, reserved) = await CountCapacityAsync(ct);

        return new ReproductionStatus(
            control.IsOpen,
            control.ClosedReason,
            waiting,
            reserved,
            Math.Max(0, waiting - reserved));
    }

    public async Task<PublicReproductionRules> GetPublicRulesAsync(CancellationToken ct = default)
    {
        var control = await LoadControlAsync(ct);

        return new PublicReproductionRules(
            control.ConceptionRatePercent,
            control.PregnancyDurationDays,
            EnumNaming.ToDbValue(control.MiscarriageMode),
            control.RulesVersion);
    }

    // ------------------------------------------------------------ audience 侍寢

    /// <summary>
    /// A player asks to attend. Whether it succeeds is decided later by an admin, because
    /// the conception roll and the heir-slot check must happen together under the control
    /// lock. Bedchamber requests are refused up front when no slot is free.
    /// </summary>
    public async Task<AudienceRequest> RequestAudienceAsync(
        Guid characterId,
        AudienceType audienceType,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var existing = await db.AudienceRequests.FirstOrDefaultAsync(
            r => r.CharacterId == characterId && r.IdempotencyKey == idempotencyKey, ct);

        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return existing;
        }

        var control = await LockAndLoadControlAsync(ct);
        control.EnsureOpen();

        var character = await LoadOwnCharacterAsync(characterId, userId, ct);
        character.EnsureCanAct();

        if (character.Role != CharacterRole.Consort)
        {
            throw DomainException.CharacterState("只有嬪妃可以申請侍奉。");
        }

        if (await db.AudienceRequests.AnyAsync(
                r => r.CharacterId == characterId
                     && (r.Status == AudienceRequestStatus.Submitted
                         || r.Status == AudienceRequestStatus.Approved),
                ct))
        {
            throw DomainException.Conflict(ErrorCodes.ConflictState, "你已有一筆待裁決的侍奉申請。");
        }

        var (waiting, reserved) = await CountCapacityAsync(ct);

        if (audienceType == AudienceType.Bedchamber)
        {
            if (await db.Pregnancies.AnyAsync(
                    p => p.MotherCharacterId == characterId && p.Status == PregnancyStatus.Ongoing, ct))
            {
                throw DomainException.Conflict(ErrorCodes.ConflictState, "此角色已有懷孕紀錄進行中。");
            }

            if (waiting - reserved <= 0)
            {
                throw DomainException.Conflict(
                    ErrorCodes.HeirCapacityExhausted, "目前沒有待生的皇嗣，無法受孕。");
            }
        }

        var request = new AudienceRequest
        {
            CharacterId = characterId,
            AudienceType = audienceType,
            Status = AudienceRequestStatus.Submitted,
            RequestedAt = now,
            IdempotencyKey = idempotencyKey,
            QualificationSnapshot = json.Serialize(new
            {
                rank = character.RankId,
                waiting,
                reserved,
                available = waiting - reserved,
                rulesVersion = control.RulesVersion
            })
        };

        db.AudienceRequests.Add(request);

        audit.Write("audience.request", "audience_request", request.Id,
            after: new { request.CharacterId, audienceType = EnumNaming.ToDbValue(audienceType) });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return request;
    }

    /// <summary>
    /// The transaction from §6.2. The admin only submits approved or rejected — they
    /// cannot set the rate, the roll, the due date or the rules version. On approval the
    /// server recomputes <c>available = waiting − ongoing</c> under the control lock, then
    /// rolls 1–100 against the published conception rate (100% by default).
    /// </summary>
    public async Task<ResolveAudienceResult> ResolveAudienceAsync(
        Guid requestId,
        long expectedVersion,
        bool approve,
        string? publicNote,
        string? privateNote,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster);

        var adminId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var control = await LockAndLoadControlAsync(ct);
        await db.LockRowAsync("audience_requests", requestId, ct);

        var request = await db.AudienceRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
                      ?? throw DomainException.NotFound("Audience request", requestId);

        request.EnsureVersion(expectedVersion);

        if (!request.IsPending)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"此申請已為 {EnumNaming.ToDbValue(request.Status)}。");
        }

        if (!approve)
        {
            request.Status = AudienceRequestStatus.Rejected;
            request.ResolvedAt = now;
            request.ResultCode = "rejected";
            request.ResultPayload = json.Serialize(new { publicNote, privateNote });

            audit.Write("audience.resolve", "audience_request", request.Id,
                after: new { status = "rejected" }, reason: privateNote);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new ResolveAudienceResult(request, null, null, null);
        }

        control.EnsureOpen();

        await db.LockCharactersAsync([request.CharacterId], ct);

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId, ct)
                        ?? throw DomainException.NotFound("Character", request.CharacterId);

        character.EnsureCanAct();

        Pregnancy? pregnancy = null;
        short? rate = null;
        short? roll = null;

        if (request.AudienceType == AudienceType.Bedchamber)
        {
            // Capacity is recomputed here, under the control lock — never read earlier and
            // reused, or two requests could both see the same free slot.
            var (waiting, reserved) = await CountCapacityAsync(ct);

            if (waiting - reserved <= 0)
            {
                throw DomainException.Conflict(
                    ErrorCodes.HeirCapacityExhausted, "目前沒有待生的皇嗣，無法保留名額。");
            }

            if (await db.Pregnancies.AnyAsync(
                    p => p.MotherCharacterId == character.Id && p.Status == PregnancyStatus.Ongoing, ct))
            {
                throw DomainException.Conflict(ErrorCodes.ConflictState, "此角色已有懷孕紀錄進行中。");
            }

            rate = control.ConceptionRatePercent;
            // 1..100 inclusive, so roll <= rate reads exactly as the spec states.
            roll = (short)(random.NextInt(100) + 1);

            var rulesSnapshot = json.Serialize(new
            {
                conceptionRatePercent = control.ConceptionRatePercent,
                pregnancyDurationDays = control.PregnancyDurationDays,
                miscarriageMode = EnumNaming.ToDbValue(control.MiscarriageMode),
                rulesVersion = control.RulesVersion
            });

            if (roll <= rate)
            {
                pregnancy = new Pregnancy
                {
                    MotherCharacterId = character.Id,
                    AudienceRequestId = request.Id,
                    Status = PregnancyStatus.Ongoing,
                    ConceivedAt = now,
                    DueAt = now.AddDays(control.PregnancyDurationDays),
                    ConceptionRatePercent = rate.Value,
                    ConceptionRoll = roll.Value,
                    SlotReservedAt = now,
                    RulesVersion = control.RulesVersion,
                    RulesSnapshot = rulesSnapshot,
                    CreatedAt = now
                };

                db.Pregnancies.Add(pregnancy);
            }
        }

        request.Status = AudienceRequestStatus.Resolved;
        request.ResolvedAt = now;
        request.ResultCode = pregnancy is not null
            ? "conceived"
            : request.AudienceType == AudienceType.Bedchamber ? "not_conceived" : "completed";
        request.ResultPayload = json.Serialize(new
        {
            conceptionRatePercent = rate,
            conceptionRoll = roll,
            pregnancyId = pregnancy?.Id,
            dueAt = pregnancy?.DueAt,
            publicNote,
            privateNote
        });

        audit.Write("audience.resolve", "audience_request", request.Id,
            after: new
            {
                status = EnumNaming.ToDbValue(request.Status),
                resultCode = request.ResultCode,
                rate,
                roll,
                pregnancyId = pregnancy?.Id
            },
            reason: privateNote);

        outbox.Enqueue("audience.resolved", "audience_request", request.Id, new
        {
            requestId = request.Id,
            characterId = character.Id,
            userId = character.UserId,
            resultCode = request.ResultCode,
            pregnancyId = pregnancy?.Id
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ResolveAudienceResult(request, pregnancy, rate, roll);
    }

    // -------------------------------------------------------------- miscarriage

    /// <summary>
    /// §6.3. In the default <c>event_only</c> mode this needs a trigger code and a
    /// verifiable source — a settled event or an allowlisted status effect — plus a
    /// private reason of at least five characters. There is no "just press the button"
    /// path, and no daily random miscarriage.
    /// </summary>
    public async Task<Pregnancy> MiscarryAsync(MiscarryInput input, CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster);

        var adminId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var control = await LockAndLoadControlAsync(ct);

        if (control.MiscarriageMode == MiscarriageMode.Disabled)
        {
            throw DomainException.Conflict(ErrorCodes.ConflictState, "流產功能目前停用。");
        }

        await db.LockRowAsync("pregnancies", input.PregnancyId, ct);

        var pregnancy = await db.Pregnancies.FirstOrDefaultAsync(p => p.Id == input.PregnancyId, ct)
                        ?? throw DomainException.NotFound("Pregnancy", input.PregnancyId);

        pregnancy.EnsureVersion(input.ExpectedVersion);

        if (control.MiscarriageMode == MiscarriageMode.EventOnly)
        {
            await EnsureMiscarriageSourceIsValidAsync(input, ct);
        }

        pregnancy.Resolve(
            PregnancyStatus.Miscarried, now, adminId, input.TriggerCode, input.PrivateReason);

        audit.Write("pregnancy.miscarry", "pregnancy", pregnancy.Id,
            after: new
            {
                status = EnumNaming.ToDbValue(pregnancy.Status),
                triggerCode = input.TriggerCode,
                sourceType = input.SourceType,
                sourceId = input.SourceId,
                pregnancy.SlotReleasedAt
            },
            reason: input.PrivateReason);

        outbox.Enqueue("pregnancy.miscarried", "pregnancy", pregnancy.Id, new
        {
            pregnancyId = pregnancy.Id,
            motherCharacterId = pregnancy.MotherCharacterId,
            publicNote = input.PublicNote
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return pregnancy;
    }

    /// <summary>
    /// event_only mode requires the miscarriage to point at something real: a settled
    /// event result or an active status effect on the mother.
    /// </summary>
    private async Task EnsureMiscarriageSourceIsValidAsync(MiscarryInput input, CancellationToken ct)
    {
        var exists = input.SourceType switch
        {
            "status_effect" => await db.StatusEffects.AnyAsync(e => e.Id == input.SourceId, ct),
            "event_result" => await db.EventResults.AnyAsync(r => r.Id == input.SourceId, ct),
            "event_room" => await db.EventRooms.AnyAsync(
                e => e.Id == input.SourceId && e.Status == EventRoomStatus.Settled, ct),
            _ => false
        };

        if (!exists)
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["sourceId"] =
                [
                    "event_only 模式下必須指向已結算事件或既有狀態效果，" +
                    "sourceType 需為 status_effect、event_result 或 event_room。"
                ]
            });
        }
    }

    // -------------------------------------------------------------- birth draw

    /// <summary>Read-only preview: candidate count and rules, without drawing anything.</summary>
    public async Task<(int CandidateCount, string RulesVersion)> PreviewBirthAsync(
        Guid pregnancyId,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster);

        var pregnancy = await db.Pregnancies.FirstOrDefaultAsync(p => p.Id == pregnancyId, ct)
                        ?? throw DomainException.NotFound("Pregnancy", pregnancyId);

        pregnancy.EnsureOngoing();

        var candidates = await db.HeirWaitPoolEntries
            .CountAsync(e => e.Status == WaitPoolStatus.Waiting, ct);

        return (candidates, pregnancy.RulesVersion);
    }

    /// <summary>
    /// §6.4. Draws one waiting heir uniformly with a CSPRNG and records enough proof —
    /// candidate count, hash of the sorted candidate ids, randomness proof, algorithm and
    /// rules version — that the draw can be audited afterwards. The request cannot name a
    /// child or a sex; the drawn prince/princess decides the sex by being who they are.
    /// </summary>
    public async Task<BirthDrawResult> DrawBirthAsync(
        Guid pregnancyId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster);

        var adminId = currentUser.UserId;
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // 1. Control lock first. Because every candidate change also takes this lock, two
        //    pregnancies can never draw the same waiting heir.
        var control = await LockAndLoadControlAsync(ct);
        control.EnsureOpen();

        // 2. Pregnancy lock.
        await db.LockRowAsync("pregnancies", pregnancyId, ct);

        var pregnancy = await db.Pregnancies.FirstOrDefaultAsync(p => p.Id == pregnancyId, ct)
                        ?? throw DomainException.NotFound("Pregnancy", pregnancyId);

        pregnancy.EnsureVersion(expectedVersion);
        pregnancy.EnsureOngoing();

        // 3. Candidate set, sorted by UUID so the hash is reproducible.
        var candidates = await db.HeirWaitPoolEntries
            .Where(e => e.Status == WaitPoolStatus.Waiting)
            .Select(e => new { e.Id, e.CharacterId })
            .ToListAsync(ct);

        var ordered = candidates.OrderBy(c => c.Id).ToList();

        if (ordered.Count == 0)
        {
            throw DomainException.Conflict(ErrorCodes.HeirCapacityExhausted, "待生池中沒有候選人。");
        }

        var candidateSetHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join(',', ordered.Select(c => c.Id.ToString("D"))))));

        // 4. Uniform CSPRNG pick, then re-verify the winner under its own lock.
        var winner = ordered[random.NextInt(ordered.Count)];

        await db.LockRowAsync("heir_wait_pool_entries", winner.Id, ct);
        await db.LockCharactersAsync([winner.CharacterId], ct);

        var poolEntry = await db.HeirWaitPoolEntries.FirstOrDefaultAsync(e => e.Id == winner.Id, ct)
                        ?? throw DomainException.NotFound("Wait pool entry", winner.Id);

        var child = await db.Characters.FirstOrDefaultAsync(c => c.Id == winner.CharacterId, ct)
                    ?? throw DomainException.NotFound("Character", winner.CharacterId);

        if (!poolEntry.IsWaiting || child.Status != CharacterStatus.WaitingBirth)
        {
            throw DomainException.Conflict(ErrorCodes.ConflictState, "抽中的候選人狀態已變更，請重試。");
        }

        // 5. Record the birth with its proof.
        var birth = new Birth
        {
            PregnancyId = pregnancy.Id,
            WaitPoolEntryId = poolEntry.Id,
            ChildCharacterId = child.Id,
            CandidateCount = ordered.Count,
            CandidateSetHash = candidateSetHash,
            RandomAlgorithm = Birth.DefaultAlgorithm,
            RandomProofHash = Convert.ToHexStringLower(SHA256.HashData(random.NextBytes(32))),
            RulesVersion = pregnancy.RulesVersion,
            DrawnBy = adminId,
            BornAt = now,
            CreatedAt = now
        };

        db.Births.Add(birth);

        // 6. Pool entry drawn, child activated, pregnancy completed and slot released.
        poolEntry.Resolve(WaitPoolStatus.Drawn, now, "birth.drawn");

        CharacterLifecycle.EnsureCanTransition(child.Status, CharacterStatus.Active);
        var previousStatus = child.Status;
        child.Status = CharacterStatus.Active;
        child.ActivatedAt = now;

        pregnancy.Resolve(PregnancyStatus.Completed, now, adminId, "birth.drawn", null);

        // 7. Parentage, status history, audit, outbox.
        db.OffspringLinks.Add(new OffspringLink
        {
            ChildCharacterId = child.Id,
            ParentType = ParentType.Mother,
            ParentCharacterId = pregnancy.MotherCharacterId,
            IsPublic = true,
            CreatedAt = now
        });

        db.CharacterStatusHistories.Add(new CharacterStatusHistory
        {
            CharacterId = child.Id,
            FromStatus = previousStatus,
            ToStatus = CharacterStatus.Active,
            ReasonCode = "birth.drawn",
            ChangedBy = adminId,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        });

        audit.Write("birth.draw", "birth", birth.Id, after: new
        {
            birth.PregnancyId,
            birth.ChildCharacterId,
            birth.CandidateCount,
            birth.CandidateSetHash,
            birth.RandomAlgorithm,
            birth.RulesVersion
        });

        outbox.Enqueue("birth.drawn", "birth", birth.Id, new
        {
            birthId = birth.Id,
            childCharacterId = child.Id,
            userId = child.UserId,
            motherCharacterId = pregnancy.MotherCharacterId
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new BirthDrawResult(birth.Id, child.Id, ordered.Count, candidateSetHash);
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>
    /// Waiting heirs and reserved slots, read together so the admin overview and the
    /// capacity check can never disagree.
    /// </summary>
    private async Task<(int Waiting, int Reserved)> CountCapacityAsync(CancellationToken ct)
    {
        var waiting = await db.HeirWaitPoolEntries.CountAsync(e => e.Status == WaitPoolStatus.Waiting, ct);
        var reserved = await db.Pregnancies.CountAsync(p => p.Status == PregnancyStatus.Ongoing, ct);

        return (waiting, reserved);
    }

    private async Task<ReproductionControl> LoadControlAsync(CancellationToken ct) =>
        await db.ReproductionControl.FirstOrDefaultAsync(
            c => c.Id == ReproductionControl.SingletonId, ct)
        ?? throw new InvalidOperationException(
            "reproduction_control row 1 is missing; the schema was not fully applied.");

    private async Task<ReproductionControl> LockAndLoadControlAsync(CancellationToken ct)
    {
        await db.LockReproductionControlAsync(ct);
        return await LoadControlAsync(ct);
    }

    private async Task<Character> LoadOwnCharacterAsync(Guid characterId, Guid userId, CancellationToken ct)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct)
                        ?? throw DomainException.NotFound("Character", characterId);

        if (character.UserId != userId)
        {
            throw DomainException.NotFound("Character", characterId);
        }

        return character;
    }
}
