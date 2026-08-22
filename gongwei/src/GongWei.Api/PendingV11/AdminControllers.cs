using GongWei.Api.Contracts;
using GongWei.Api.Http;
using GongWei.Application.Abstractions;
using GongWei.Application.Characters;
using GongWei.Application.Events;
using GongWei.Application.Identity;
using GongWei.Application.Operations;
using GongWei.Application.Reproduction;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Controllers;

/// <summary>
/// Admin endpoints on the player API. The management *web UI* is a separate IIS site
/// (GongWei.Admin) with its own cookie and no CORS; these exist so the admin site and
/// tooling share one implementation rather than two (spec §2.2).
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize]
public sealed class AdminApplicationsController(
    CharacterApplicationService applications,
    PortraitService portraits,
    GongWeiDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<ApplicationReviewResponse>>> Queue(
        [FromQuery] string status = "submitted",
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.CharacterReviewer);

        var parsed = EnumNaming.FromDbValue<ApplicationStatus>(status);

        var rows = await db.CharacterApplications
            .Where(a => a.Status == parsed)
            .OrderBy(a => a.SubmittedAt)
            .Take(200)
            .Select(a => new
            {
                Application = a,
                PlayerName = a.User!.DisplayName,
                PortraitStatus = a.PlayerPortraitSubmission == null
                    ? (PortraitReviewStatus?)null
                    : a.PlayerPortraitSubmission.ReviewStatus
            })
            .ToListAsync(ct);

        return Ok(rows.Select(r => new ApplicationReviewResponse(
            ApplicationResponse.From(r.Application),
            r.Application.UserId,
            r.PlayerName,
            r.Application.ClaimedBy,
            r.Application.ClaimedAt,
            r.PortraitStatus is null ? null : EnumNaming.ToDbValue(r.PortraitStatus.Value))).ToList());
    }

    [HttpPost("applications/{id:guid}/claim")]
    public async Task<ActionResult<ApplicationResponse>> Claim(Guid id, CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var application = await applications.ClaimAsync(id, version, ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    [HttpPost("applications/{id:guid}/request-revision")]
    public async Task<ActionResult<ApplicationResponse>> RequestRevision(
        Guid id,
        [FromBody] ReviewNoteRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var application = await applications.RequestRevisionAsync(id, version, request.Note, ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    [HttpPost("applications/{id:guid}/reject")]
    public async Task<ActionResult<ApplicationResponse>> Reject(
        Guid id,
        [FromBody] ReviewNoteRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var application = await applications.RejectAsync(id, version, request.Note, ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    [HttpPost("applications/{id:guid}/approve")]
    [RequireIdempotency]
    public async Task<ActionResult<CharacterSummaryResponse>> Approve(
        Guid id,
        [FromBody] ApproveApplicationRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);

        var character = await applications.ApproveAsync(
            new ApproveApplicationInput(
                id, version, request.RankId, request.ResidenceId,
                request.Charm, request.Intellect, request.Artistry, request.Stamina, request.Note),
            ct);

        var loaded = await db.Characters
            .Include(c => c.Rank)
            .Include(c => c.Residence)
            .Include(c => c.PresetPortrait)
            .FirstAsync(c => c.Id == character.Id, ct);

        Response.Headers.ETag = ETagHelper.Format(loaded.Version);
        return Ok(CharacterSummaryResponse.From(loaded, AuthController.PortraitUrl(loaded), null));
    }

    [HttpGet("portraits")]
    public async Task<ActionResult<IReadOnlyList<PortraitSubmissionResponse>>> PortraitQueue(
        CancellationToken ct)
    {
        currentUser.RequireRole(AdminRole.CharacterReviewer, AdminRole.CharacterManager);

        var pending = await db.PlayerPortraitSubmissions
            .Where(s => s.ReviewStatus == PortraitReviewStatus.Pending)
            .OrderBy(s => s.SubmittedAt)
            .Take(200)
            .ToListAsync(ct);

        return Ok(pending.Select(PortraitSubmissionResponse.From).ToList());
    }

    [HttpPost("portraits/{id:guid}/review")]
    public async Task<ActionResult<PortraitSubmissionResponse>> ReviewPortrait(
        Guid id,
        [FromBody] ReviewPortraitRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var submission = await portraits.ReviewAsync(id, version, request.Approve, request.Note, ct);

        Response.Headers.ETag = ETagHelper.Format(submission.Version);
        return Ok(PortraitSubmissionResponse.From(submission));
    }
}

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public sealed class AdminGameplayController(
    EventService events,
    ReproductionService reproduction,
    GongWeiDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Dry run — writes nothing, so a GM can check a settlement before committing.</summary>
    [HttpPost("events/{id:guid}/settlement/preview")]
    public async Task<ActionResult<SettlementPreview>> PreviewSettlement(
        Guid id,
        [FromBody] SettlementRequest request,
        CancellationToken ct)
    {
        var preview = await events.PreviewSettlementAsync(
            new SettlementInput(id, 0, request.GlobalNarrative, request.Characters), ct);

        return Ok(preview);
    }

    [HttpPost("events/{id:guid}/settlement")]
    [RequireIdempotency]
    public async Task<IActionResult> ExecuteSettlement(
        Guid id,
        [FromBody] SettlementRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);

        var affected = await events.ExecuteSettlementAsync(
            new SettlementInput(id, version, request.GlobalNarrative, request.Characters), ct);

        return Ok(new { affectedCharacters = affected });
    }

    [HttpPost("events/{id:guid}/status/{target}")]
    public async Task<IActionResult> ChangeStatus(Guid id, string target, CancellationToken ct)
    {
        currentUser.RequireRole(AdminRole.GameMaster);

        var version = ETagHelper.RequireIfMatch(Request);
        var desired = EnumNaming.FromDbValue<EventRoomStatus>(target);

        if (desired is not (EventRoomStatus.Scheduled or EventRoomStatus.Open
            or EventRoomStatus.Locked or EventRoomStatus.Cancelled))
        {
            throw DomainException.Validation(
                ErrorCodes.ValidationFailed,
                "Only scheduled, open, locked and cancelled can be set directly; settlement has its own endpoint.");
        }

        var room = await db.EventRooms.FirstOrDefaultAsync(e => e.Id == id, ct)
                   ?? throw DomainException.NotFound("Event", id);

        room.EnsureVersion(version);

        if (room.Status == EventRoomStatus.Settled)
        {
            throw DomainException.Conflict(
                ErrorCodes.EventAlreadySettled, "A settled event cannot change status.");
        }

        room.Status = desired;
        room.Touch(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        Response.Headers.ETag = ETagHelper.Format(room.Version);
        return Ok(new { status = EnumNaming.ToDbValue(room.Status), version = room.Version });
    }

    [HttpGet("audiences")]
    public async Task<IActionResult> AudienceQueue(CancellationToken ct)
    {
        currentUser.RequireRole(AdminRole.GameMaster);

        var pending = await db.AudienceRequests
            .Where(r => r.Status == AudienceStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                r.CharacterId,
                CharacterName = r.Character!.DisplayName,
                Kind = r.Kind,
                r.RequestedAt,
                r.Version
            })
            .ToListAsync(ct);

        return Ok(pending.Select(r => new
        {
            r.Id,
            r.CharacterId,
            r.CharacterName,
            kind = EnumNaming.ToDbValue(r.Kind),
            r.RequestedAt,
            r.Version
        }));
    }

    [HttpPost("audiences/{id:guid}/resolve")]
    [RequireIdempotency]
    public async Task<IActionResult> ResolveAudience(
        Guid id,
        [FromBody] ResolveAudienceRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);

        var (audienceRequest, pregnancy) = await reproduction.ResolveAudienceAsync(
            id, version, request.ForceOutcome, request.Note, ct);

        return Ok(new
        {
            status = EnumNaming.ToDbValue(audienceRequest.Status),
            roll = audienceRequest.SuccessRoll,
            threshold = audienceRequest.SuccessThreshold,
            pregnancy = pregnancy is null ? null : PregnancyResponse.From(pregnancy)
        });
    }

    [HttpPost("pregnancies/{id:guid}/miscarry")]
    [RequireIdempotency]
    public async Task<ActionResult<PregnancyResponse>> Miscarry(
        Guid id,
        [FromBody] MiscarryRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var pregnancy = await reproduction.MiscarryAsync(id, version, request.Reason, ct);

        Response.Headers.ETag = ETagHelper.Format(pregnancy.Version);
        return Ok(PregnancyResponse.From(pregnancy));
    }

    /// <summary>The draw takes no child or sex parameter — that is the whole point (spec §6.4).</summary>
    [HttpPost("pregnancies/{id:guid}/draw-birth")]
    [RequireIdempotency]
    public async Task<ActionResult<BirthDrawResult>> DrawBirth(Guid id, CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var result = await reproduction.DrawBirthAsync(id, version, ct);

        return Ok(result);
    }
}

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public sealed class AdminOperationsController(
    ApprovalService approvals,
    GongWeiDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        currentUser.RequireRole(
            AdminRole.CharacterReviewer, AdminRole.GameMaster, AdminRole.EconomyManager,
            AdminRole.Moderator, AdminRole.Auditor, AdminRole.ContentEditor,
            AdminRole.CharacterManager, AdminRole.SystemConfigManager);

        var now = DateTimeOffset.UtcNow;

        return Ok(new
        {
            pendingApplications = await db.CharacterApplications
                .CountAsync(a => a.Status == ApplicationStatus.Submitted, ct),
            pendingPortraits = await db.PlayerPortraitSubmissions
                .CountAsync(s => s.ReviewStatus == PortraitReviewStatus.Pending, ct),
            pendingAudiences = await db.AudienceRequests
                .CountAsync(r => r.Status == AudienceStatus.Pending, ct),
            pendingExternalPlay = await db.ExternalPlaySubmissions
                .CountAsync(s => s.ReviewStatus == ExternalPlayReviewStatus.Pending, ct),
            pendingApprovals = await db.ApprovalRequests
                .CountAsync(r => r.Status == ApprovalStatus.Pending, ct),
            openEvents = await db.EventRooms
                .CountAsync(e => e.Status == EventRoomStatus.Open, ct),
            duePregnancies = await db.Pregnancies
                .CountAsync(p => p.Status == PregnancyStatus.Ongoing && p.DueAt <= now, ct),
            activeCharacters = await db.Characters
                .CountAsync(c => c.Status == CharacterStatus.Active, ct),
            waitingHeirs = await db.HeirWaitPoolEntries
                .CountAsync(e => e.Status == WaitPoolStatus.Waiting, ct),
            stuckOutbox = await db.OutboxMessages
                .CountAsync(m => m.Status == OutboxStatus.Dead, ct)
        });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(
        [FromQuery] string? targetType,
        [FromQuery] Guid? targetId,
        [FromQuery] long? beforeId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.Auditor);

        limit = Math.Clamp(limit, 1, 200);

        var query = db.AuditLogs.AsQueryable();

        if (targetType is not null)
        {
            query = query.Where(a => a.TargetType == targetType);
        }

        if (targetId is { } id)
        {
            query = query.Where(a => a.TargetId == id);
        }

        if (beforeId is { } cursor)
        {
            query = query.Where(a => a.Id < cursor);
        }

        var rows = await query
            .OrderByDescending(a => a.Id)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.OccurredAt,
                a.ActorUserId,
                a.ActorRole,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Reason,
                a.RequestId
            })
            .ToListAsync(ct);

        return Ok(new { items = rows, nextCursor = rows.Count == limit ? rows[^1].Id : (long?)null });
    }

    [HttpGet("approvals")]
    public async Task<IActionResult> Approvals(CancellationToken ct)
    {
        currentUser.RequireRole(
            AdminRole.GameMaster, AdminRole.EconomyManager, AdminRole.Moderator,
            AdminRole.Auditor, AdminRole.SystemConfigManager);

        var pending = await db.ApprovalRequests
            .Where(r => r.Status == ApprovalStatus.Pending || r.Status == ApprovalStatus.Approved)
            .OrderBy(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                Handler = r.ActionHandler,
                Status = r.Status,
                r.Reason,
                r.TargetType,
                r.TargetId,
                r.RequestedBy,
                r.RequestedAt,
                r.ExpiresAt,
                r.Version
            })
            .ToListAsync(ct);

        return Ok(pending.Select(r => new
        {
            r.Id,
            handler = EnumNaming.ToDbValue(r.Handler),
            status = EnumNaming.ToDbValue(r.Status),
            r.Reason,
            r.TargetType,
            r.TargetId,
            r.RequestedBy,
            r.RequestedAt,
            r.ExpiresAt,
            r.Version
        }));
    }

    [HttpPost("approvals")]
    [RequireIdempotency]
    public async Task<IActionResult> CreateApproval(
        [FromBody] CreateApprovalRequestBody request,
        CancellationToken ct)
    {
        var created = await approvals.CreateAsync(
            new CreateApprovalInput(
                request.Handler, request.Reason, request.PayloadJson,
                request.TargetType, request.TargetId, request.TargetVersion, null),
            ct);

        Response.Headers.ETag = ETagHelper.Format(created.Version);

        return Created($"/api/v1/admin/approvals/{created.Id}", new
        {
            id = created.Id,
            handler = EnumNaming.ToDbValue(created.ActionHandler),
            status = EnumNaming.ToDbValue(created.Status),
            created.ExpiresAt,
            created.Version
        });
    }

    /// <summary>The second person's decision. The requester is rejected by the domain rule.</summary>
    [HttpPost("approvals/{id:guid}/decision")]
    public async Task<IActionResult> Decide(
        Guid id,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var updated = await approvals.DecideAsync(id, version, request.Decision, request.Note, ct);

        Response.Headers.ETag = ETagHelper.Format(updated.Version);
        return Ok(new { status = EnumNaming.ToDbValue(updated.Status), version = updated.Version });
    }

    [HttpPost("approvals/{id:guid}/execute")]
    [RequireIdempotency]
    public async Task<IActionResult> Execute(Guid id, CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        await approvals.ExecuteAsync(id, version, ct);

        return NoContent();
    }
}
