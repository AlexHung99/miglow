using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using GongWei.Application.Abstractions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Economy;
using GongWei.Domain.Identity;
using GongWei.Domain.Reproduction;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Characters;

/// <summary>The four abilities a starting 位號 grants, stored in <c>ranks.initial_stats</c>.</summary>
public sealed record InitialStats(
    [property: JsonPropertyName("vitality")] short Vitality,
    [property: JsonPropertyName("appearance")] short Appearance,
    [property: JsonPropertyName("strategy")] short Strategy,
    [property: JsonPropertyName("luck")] short Luck);

/// <summary>Build-a-character form fields. Sex is derived from role, never supplied (§13.1).</summary>
public sealed record ApplicationFormInput(
    CharacterRole Role,
    string FamilyName,
    string GivenName,
    string? CourtesyName,
    string? BirthDateLabel,
    short? Age,
    string Appearance,
    string Biography,
    string Personality,
    string Strengths,
    string Weaknesses,
    string Likes,
    string Dislikes,
    Guid? PortraitId,
    Guid? PlayerPortraitSubmissionId,
    string? FormDataJson);

/// <summary>Reviewer scores: 字數 35%、文筆 50%、邏輯 15% (rank_catalog_v1.0 §7).</summary>
public sealed record ApplicationScores(int WordCount, int Writing, int Logic);

public sealed record ApproveApplicationInput(
    Guid ApplicationId,
    long ExpectedVersion,
    Guid InitialRankId,
    Guid? ResidenceId,
    ApplicationScores? Scores,
    string? ReviewNote);

/// <summary>
/// Build-a-character: the player's draft/submit side and the reviewer's decision side,
/// including the approval transaction from §6.1.
/// </summary>
public sealed class CharacterApplicationService(
    IGongWeiDb db,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOutboxWriter outbox,
    IJsonSerializer json)
{
    // ------------------------------------------------------------------ player

    public async Task<CharacterApplication> CreateDraftAsync(
        ApplicationFormInput input,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await EnsureNoCurrentCharacterAsync(userId, ct);

        if (await db.CharacterApplications.AnyAsync(
                a => a.UserId == userId
                     && (a.Status == ApplicationStatus.Draft
                         || a.Status == ApplicationStatus.Submitted
                         || a.Status == ApplicationStatus.NeedsRevision),
                ct))
        {
            throw DomainException.Conflict(
                ErrorCodes.OpenApplicationExists, "你已有一份進行中的建角申請。");
        }

        var application = new CharacterApplication
        {
            UserId = userId,
            Status = ApplicationStatus.Draft,
            CreatedAt = now
        };

        await ApplyFormAsync(application, input, ct);

        db.CharacterApplications.Add(application);
        await WriteRevisionAsync(application, userId, "created", now, ct);

        audit.Write("character_application.create", "character_application", application.Id,
            after: Snapshot(application));
        await db.SaveChangesAsync(ct);

        return application;
    }

    public async Task<CharacterApplication> UpdateDraftAsync(
        Guid applicationId,
        long expectedVersion,
        ApplicationFormInput input,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var application = await LoadOwnAsync(applicationId, userId, ct);
        application.EnsureVersion(expectedVersion);

        if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.NeedsRevision))
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"目前狀態為 {EnumNaming.ToDbValue(application.Status)}，無法編輯。");
        }

        var before = Snapshot(application);
        await ApplyFormAsync(application, input, ct);
        await WriteRevisionAsync(application, userId, "edited", now, ct);

        audit.Write("character_application.update", "character_application", application.Id,
            before, Snapshot(application));
        await db.SaveChangesAsync(ct);

        return application;
    }

    public async Task<CharacterApplication> SubmitAsync(
        Guid applicationId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var application = await LoadOwnAsync(applicationId, userId, ct);
        application.EnsureVersion(expectedVersion);

        ApplicationLifecycle.EnsureCanTransition(application.Status, ApplicationStatus.Submitted);

        // Full field validation only bites here — a draft may stay incomplete (§0.2).
        application.EnsureReadyForSubmission();

        await EnsureNoCurrentCharacterAsync(userId, ct);
        await EnsurePortraitUsableAsync(application, requireApproved: false, ct);

        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAt = now;

        await WriteRevisionAsync(application, userId, "submitted", now, ct);

        audit.Write("character_application.submit", "character_application", application.Id,
            after: Snapshot(application));
        outbox.Enqueue("character_application.submitted", "character_application", application.Id,
            new { applicationId = application.Id, userId });

        await db.SaveChangesAsync(ct);
        return application;
    }

    public async Task<CharacterApplication> CancelAsync(
        Guid applicationId,
        long expectedVersion,
        string? reason,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var application = await LoadOwnAsync(applicationId, userId, ct);
        application.EnsureVersion(expectedVersion);

        ApplicationLifecycle.EnsureCanTransition(application.Status, ApplicationStatus.Cancelled);

        application.Status = ApplicationStatus.Cancelled;
        application.ReviewNote = reason;

        audit.Write("character_application.cancel", "character_application", application.Id,
            reason: reason);
        await db.SaveChangesAsync(ct);

        return application;
    }

    // ------------------------------------------------------------------- admin

    public async Task<CharacterApplication> RequestRevisionAsync(
        Guid applicationId,
        long expectedVersion,
        string note,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.CharacterReviewer);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var application = await LoadAsync(applicationId, ct);
        application.EnsureVersion(expectedVersion);

        ApplicationLifecycle.EnsureCanTransition(application.Status, ApplicationStatus.NeedsRevision);

        application.Status = ApplicationStatus.NeedsRevision;
        application.ReviewNote = note;
        application.ReviewedBy = reviewerId;
        application.ReviewedAt = now;

        audit.Write("character_application.request_revision", "character_application",
            application.Id, reason: note);
        outbox.Enqueue("character_application.needs_revision", "character_application", application.Id,
            new { applicationId = application.Id, application.UserId, note });

        await db.SaveChangesAsync(ct);
        return application;
    }

    public async Task<CharacterApplication> RejectAsync(
        Guid applicationId,
        long expectedVersion,
        string reason,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.CharacterReviewer);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var application = await LoadAsync(applicationId, ct);
        application.EnsureVersion(expectedVersion);

        ApplicationLifecycle.EnsureCanTransition(application.Status, ApplicationStatus.Rejected);

        application.Status = ApplicationStatus.Rejected;
        application.ReviewedBy = reviewerId;
        application.ReviewedAt = now;
        application.ReviewNote = reason;

        audit.Write("character_application.reject", "character_application", application.Id,
            reason: reason);
        outbox.Enqueue("character_application.rejected", "character_application", application.Id,
            new { applicationId = application.Id, application.UserId, reason });

        await db.SaveChangesAsync(ct);
        return application;
    }

    /// <summary>
    /// Approving the form and creating the character is one transaction (§6.1). Abilities
    /// come from the chosen starting 位號 — the request cannot supply stats, currencies or
    /// action points (§13.1). Prestige, favor and silver all start at 0 (§0.2).
    /// </summary>
    public async Task<Character> ApproveAsync(ApproveApplicationInput input, CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.CharacterReviewer);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // 1. Lock the application and its owner so a concurrent approve cannot slip a
        //    second character past the one-character rule.
        await db.LockRowAsync("character_applications", input.ApplicationId, ct);

        var application = await LoadAsync(input.ApplicationId, ct);
        await db.LockRowAsync("users", application.UserId, ct);

        // 2. Re-validate under the lock.
        application.EnsureVersion(input.ExpectedVersion);

        if (application.Status != ApplicationStatus.Submitted)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"只有已送審的申請可以核准，目前為 {EnumNaming.ToDbValue(application.Status)}。");
        }

        application.EnsureReadyForSubmission();
        await EnsureNoCurrentCharacterAsync(application.UserId, ct);
        await EnsurePortraitUsableAsync(application, requireApproved: true, ct);

        // 3. The starting rank decides the abilities.
        var rank = await db.Ranks.FirstOrDefaultAsync(
                       r => r.Id == input.InitialRankId && r.IsActive, ct)
                   ?? throw DomainException.NotFound("Rank", input.InitialRankId);

        if (!rank.IsApplicationOption)
        {
            throw DomainException.Validation("此位號不可作為建角起始位號。");
        }

        if (rank.AppliesToRole != application.Role)
        {
            throw DomainException.Validation(
                $"位號「{rank.DisplayName}」不適用於 {EnumNaming.ToDbValue(application.Role)}。");
        }

        var stats = ParseInitialStats(rank);
        var residence = await ResolveResidenceAsync(input.ResidenceId, ct);

        // 4. Character, stats, wallet and the opening history rows.
        var status = CharacterLifecycle.InitialStatusFor(application.Role);

        var character = new Character
        {
            UserId = application.UserId,
            SourceApplicationId = application.Id,
            Role = application.Role,
            Sex = application.Sex,
            FamilyName = application.FamilyName,
            GivenName = application.GivenName,
            CourtesyName = application.CourtesyName,
            BirthDateLabel = application.BirthDateLabel,
            AgeAtCreation = application.Age ?? 0,
            Appearance = application.Appearance,
            Biography = application.Biography,
            Personality = application.Personality,
            Strengths = application.Strengths,
            Weaknesses = application.Weaknesses,
            Likes = application.Likes,
            Dislikes = application.Dislikes,
            PortraitId = application.PortraitId,
            PlayerPortraitSubmissionId = application.PlayerPortraitSubmissionId,
            RankId = rank.Id,
            ResidenceId = residence?.Id,
            Status = status,
            ActivatedAt = status == CharacterStatus.Active ? now : null,
            CreatedAt = now
        };

        db.Characters.Add(character);

        var characterStats = new CharacterStats
        {
            CharacterId = character.Id,
            Vitality = stats.Vitality,
            Appearance = stats.Appearance,
            Strategy = stats.Strategy,
            Luck = stats.Luck,
            // 威望、恩寵與銀兩皆從 0 開始 (§0.2).
            Prestige = 0,
            Favor = 0
        };
        characterStats.EnsureInRange();
        db.CharacterStats.Add(characterStats);

        db.Wallets.Add(new Wallet
        {
            CharacterId = character.Id,
            CurrencyCode = Currency.Silver,
            Balance = 0
        });

        db.CharacterStatusHistories.Add(new CharacterStatusHistory
        {
            CharacterId = character.Id,
            FromStatus = null,
            ToStatus = status,
            ReasonCode = "character_application.approved",
            ChangedBy = reviewerId,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        });

        db.RankHistories.Add(new RankHistory
        {
            CharacterId = character.Id,
            FromRankId = null,
            ToRankId = rank.Id,
            ReasonCode = "character_application.approved",
            ChangedBy = reviewerId,
            EffectiveAt = now,
            CreatedAt = now
        });

        if (residence is not null)
        {
            db.CharacterResidenceHistories.Add(new CharacterResidenceHistory
            {
                CharacterId = character.Id,
                ResidenceId = residence.Id,
                MovedInAt = now,
                Reason = "character_application.approved",
                ChangedBy = reviewerId
            });
        }

        // 5. Princes and princesses start life in the wait pool (§6.1 step 5).
        if (status == CharacterStatus.WaitingBirth)
        {
            await db.LockReproductionControlAsync(ct);

            db.HeirWaitPoolEntries.Add(new HeirWaitPoolEntry
            {
                CharacterId = character.Id,
                Status = WaitPoolStatus.Waiting,
                EnteredAt = now,
                CreatedBy = reviewerId
            });
        }

        // 6. Close the application. Scores stay in the revision and the audit trail.
        application.Status = ApplicationStatus.Approved;
        application.ReviewedBy = reviewerId;
        application.ReviewedAt = now;
        application.ReviewNote = input.ReviewNote;
        application.CreatedCharacterId = character.Id;

        await WriteRevisionAsync(application, reviewerId, "approved", now, ct, input.Scores);

        audit.Write("character_application.approve", "character", character.Id,
            after: new
            {
                characterId = character.Id,
                applicationId = application.Id,
                rank = rank.DisplayName,
                role = EnumNaming.ToDbValue(character.Role),
                status = EnumNaming.ToDbValue(character.Status),
                initialStats = stats,
                scores = input.Scores
            },
            reason: input.ReviewNote);

        outbox.Enqueue("character.created", "character", character.Id, new
        {
            characterId = character.Id,
            application.UserId,
            role = EnumNaming.ToDbValue(character.Role),
            status = EnumNaming.ToDbValue(character.Status)
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return character;
    }

    // ------------------------------------------------------------------ helpers

    private static InitialStats ParseInitialStats(Rank rank)
    {
        if (string.IsNullOrWhiteSpace(rank.InitialStats))
        {
            throw DomainException.Validation(
                $"位號「{rank.DisplayName}」沒有設定初始能力，無法作為建角起始位號。");
        }

        var stats = JsonSerializer.Deserialize<InitialStats>(rank.InitialStats)
                    ?? throw DomainException.Validation(
                        $"位號「{rank.DisplayName}」的初始能力資料格式錯誤。");

        return stats;
    }

    private async Task<CharacterApplication> LoadAsync(Guid id, CancellationToken ct) =>
        await db.CharacterApplications.FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw DomainException.NotFound("Character application", id);

    private async Task<CharacterApplication> LoadOwnAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var application = await LoadAsync(id, ct);

        // 404 rather than 403 keeps other players' application ids unguessable (§1.4).
        if (application.UserId != userId)
        {
            throw DomainException.NotFound("Character application", id);
        }

        return application;
    }

    private async Task EnsureNoCurrentCharacterAsync(Guid userId, CancellationToken ct)
    {
        var hasCurrent = await db.Characters.AnyAsync(
            c => c.UserId == userId
                 && (c.Status == CharacterStatus.WaitingBirth
                     || c.Status == CharacterStatus.Active
                     || c.Status == CharacterStatus.Paused
                     || c.Status == CharacterStatus.Suspended),
            ct);

        if (hasCurrent)
        {
            throw DomainException.Conflict(
                ErrorCodes.CurrentCharacterExists, "此帳號已有一名目前角色。");
        }
    }

    private async Task ApplyFormAsync(
        CharacterApplication application,
        ApplicationFormInput input,
        CancellationToken ct)
    {
        if (input.PortraitId is not null && input.PlayerPortraitSubmissionId is not null)
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["portrait"] = ["只能選擇官方立繪或自行上傳的圖片其中一種"]
            });
        }

        application.Role = input.Role;
        // Derived, never accepted from the request, so role and sex cannot disagree.
        application.Sex = CharacterApplication.SexFor(input.Role);
        application.FamilyName = input.FamilyName.Trim();
        application.GivenName = input.GivenName.Trim();
        application.CourtesyName = input.CourtesyName?.Trim();
        application.BirthDateLabel = input.BirthDateLabel?.Trim();
        application.Age = input.Age;
        application.Appearance = input.Appearance.Trim();
        application.Biography = input.Biography.Trim();
        application.Personality = input.Personality.Trim();
        application.Strengths = input.Strengths.Trim();
        application.Weaknesses = input.Weaknesses.Trim();
        application.Likes = input.Likes.Trim();
        application.Dislikes = input.Dislikes.Trim();
        application.FormData = NormaliseFormData(input.FormDataJson);

        application.PortraitId = null;
        application.PlayerPortraitSubmissionId = null;

        if (input.PortraitId is { } presetId)
        {
            var portrait = await db.PresetPortraits.FirstOrDefaultAsync(
                               p => p.Id == presetId && p.IsActive, ct)
                           ?? throw DomainException.NotFound("Preset portrait", presetId);

            if (portrait.Role != input.Role)
            {
                throw DomainException.Validation(
                    $"該立繪適用於 {EnumNaming.ToDbValue(portrait.Role)}，與所選身分不符。");
            }

            application.PortraitId = portrait.Id;
        }

        if (input.PlayerPortraitSubmissionId is { } submissionId)
        {
            var submission = await db.PlayerPortraitSubmissions
                                 .FirstOrDefaultAsync(s => s.Id == submissionId, ct)
                             ?? throw DomainException.NotFound("Portrait upload", submissionId);

            if (submission.UserId != application.UserId)
            {
                throw DomainException.NotFound("Portrait upload", submissionId);
            }

            if (submission.Role != input.Role)
            {
                throw DomainException.Validation(
                    $"該上傳圖片申請的身分為 {EnumNaming.ToDbValue(submission.Role)}，與所選身分不符。");
            }

            application.PlayerPortraitSubmissionId = submission.Id;
        }
    }

    /// <summary>
    /// An uploaded portrait can be rejected after the form referenced it, so approval
    /// re-checks it rather than trusting what the draft recorded (§6.8 step 6).
    /// </summary>
    private async Task EnsurePortraitUsableAsync(
        CharacterApplication application,
        bool requireApproved,
        CancellationToken ct)
    {
        if (application.PlayerPortraitSubmissionId is not { } id)
        {
            return;
        }

        var submission = await db.PlayerPortraitSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct)
                         ?? throw DomainException.NotFound("Portrait upload", id);

        if (submission.Status is PortraitSubmissionStatus.Rejected or PortraitSubmissionStatus.Withdrawn)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"所引用的人物圖片已 {EnumNaming.ToDbValue(submission.Status)}，請重新選擇。");
        }

        if (requireApproved && !submission.IsUsableForCharacter)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, "人物圖片尚未通過審核，無法建立角色。");
        }
    }

    private async Task<Domain.Characters.Residence?> ResolveResidenceAsync(
        Guid? residenceId,
        CancellationToken ct)
    {
        if (residenceId is not { } id)
        {
            return null;
        }

        var residence = await db.Residences.FirstOrDefaultAsync(r => r.Id == id, ct)
                        ?? throw DomainException.NotFound("Residence", id);

        // Occupancy is counted under the transaction's locks, not from a cached value.
        var occupancy = await db.CharacterResidenceHistories
            .CountAsync(h => h.ResidenceId == id && h.MovedOutAt == null, ct);

        residence.EnsureHasRoom(occupancy);
        return residence;
    }

    private async Task WriteRevisionAsync(
        CharacterApplication application,
        Guid changedBy,
        string reason,
        DateTimeOffset now,
        CancellationToken ct,
        ApplicationScores? scores = null)
    {
        var next = await db.CharacterApplicationRevisions
            .Where(r => r.ApplicationId == application.Id)
            .MaxAsync(r => (int?)r.RevisionNo, ct) ?? 0;

        db.CharacterApplicationRevisions.Add(new CharacterApplicationRevision
        {
            ApplicationId = application.Id,
            RevisionNo = next + 1,
            Snapshot = json.Serialize(new { application = Snapshot(application), scores }),
            ChangedBy = changedBy,
            ChangeReason = reason,
            CreatedAt = now
        });
    }

    private string NormaliseFormData(string? formDataJson)
    {
        if (string.IsNullOrWhiteSpace(formDataJson))
        {
            return "{}";
        }

        if (!json.IsValidObject(formDataJson))
        {
            throw DomainException.Validation("formData 必須是 JSON 物件。");
        }

        return formDataJson;
    }

    private static object Snapshot(CharacterApplication a) => new
    {
        a.Id,
        status = EnumNaming.ToDbValue(a.Status),
        role = EnumNaming.ToDbValue(a.Role),
        sex = EnumNaming.ToDbValue(a.Sex),
        a.FamilyName,
        a.GivenName,
        a.CourtesyName,
        a.BirthDateLabel,
        a.Age,
        a.Appearance,
        a.Biography,
        a.Personality,
        a.Strengths,
        a.Weaknesses,
        a.Likes,
        a.Dislikes,
        a.PortraitId,
        a.PlayerPortraitSubmissionId,
        a.FormData,
        a.Version
    };
}
