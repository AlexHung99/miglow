using GongWei.Api.Contracts;
using GongWei.Api.Http;
using GongWei.Application.Abstractions;
using GongWei.Application.Characters;
using GongWei.Application.Identity;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(GongWeiDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw DomainException.NotFound("User", userId);

        var character = await CurrentCharacterQuery(db, userId).FirstOrDefaultAsync(ct);

        return Ok(new MeResponse(
            user.Id,
            user.DisplayName,
            user.AvatarUrl,
            currentUser.AdminRoles.Select(EnumNaming.ToDbValue).ToList(),
            character is null ? null : CharacterSummaryResponse.From(character, AuthController.PortraitUrl(character), null)));
    }

    /// <summary>The player's own character, including stats and wallets.</summary>
    [HttpGet("character")]
    public async Task<ActionResult<CharacterPrivateResponse>> GetCharacter(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var character = await CurrentCharacterQuery(db, userId)
                            .Include(c => c.Stats)
                            .FirstOrDefaultAsync(ct)
                        ?? throw DomainException.Conflict(
                            ErrorCodes.NoActiveCharacter, "This account has no current character.");

        var wallets = await db.Wallets
            .Where(w => w.CharacterId == character.Id)
            .Join(db.Currencies, w => w.CurrencyCode, c => c.Code,
                (w, c) => new WalletResponse(w.CurrencyCode, c.DisplayName, w.Balance))
            .ToListAsync(ct);

        var primaryTitle = await db.CharacterTitleAssignments
            .Where(t => t.CharacterId == character.Id && t.IsPrimary && t.RevokedAt == null)
            .Select(t => t.TitleDefinition!.DisplayName)
            .FirstOrDefaultAsync(ct);

        Response.Headers.ETag = ETagHelper.Format(character.Version);

        return Ok(new CharacterPrivateResponse(
            CharacterSummaryResponse.From(character, AuthController.PortraitUrl(character), primaryTitle),
            character.Biography,
            character.Appearance,
            character.Personality,
            character.Stats is null
                ? null
                : new CharacterStatsResponse(
                    character.Stats.Charm,
                    character.Stats.Intellect,
                    character.Stats.Artistry,
                    character.Stats.Stamina,
                    character.Stats.Favor,
                    character.Stats.Reputation,
                    character.Stats.ActionPoints,
                    character.Stats.ActionPointsMax),
            wallets));
    }

    internal static IQueryable<Domain.Characters.Character> CurrentCharacterQuery(
        GongWeiDbContext db,
        Guid userId) =>
        db.Characters
            .Include(c => c.Rank)
            .Include(c => c.Residence)
            .Include(c => c.PresetPortrait)
            .Where(c => c.UserId == userId
                        && (c.Status == CharacterStatus.WaitingBirth
                            || c.Status == CharacterStatus.Active
                            || c.Status == CharacterStatus.Paused
                            || c.Status == CharacterStatus.Suspended));
}

[ApiController]
[Route("api/v1/characters")]
public sealed class CharactersController(GongWeiDbContext db) : ControllerBase
{
    /// <summary>
    /// The public roster. Dead characters stay listed — the palace remembers — but
    /// archived ones drop out.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CharacterSummaryResponse>>> List(
        [FromQuery] string? role,
        CancellationToken ct = default)
    {
        var query = db.Characters
            .Include(c => c.Rank)
            .Include(c => c.Residence)
            .Include(c => c.PresetPortrait)
            .Where(c => c.Status != CharacterStatus.Archived
                        && c.Status != CharacterStatus.WaitingBirth);

        if (role is not null)
        {
            var parsed = EnumNaming.FromDbValue<CharacterRole>(role);
            query = query.Where(c => c.Role == parsed);
        }

        var characters = await query
            .OrderBy(c => c.Rank!.Ordinal)
            .ThenBy(c => c.DisplayName)
            .Take(500)
            .ToListAsync(ct);

        return Ok(characters
            .Select(c => CharacterSummaryResponse.From(c, AuthController.PortraitUrl(c), null))
            .ToList());
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CharacterSummaryResponse>> Get(Guid id, CancellationToken ct)
    {
        var character = await db.Characters
                            .Include(c => c.Rank)
                            .Include(c => c.Residence)
                            .Include(c => c.PresetPortrait)
                            .FirstOrDefaultAsync(c => c.Id == id && c.Status != CharacterStatus.Archived, ct)
                        ?? throw DomainException.NotFound("Character", id);

        var primaryTitle = await db.CharacterTitleAssignments
            .Where(t => t.CharacterId == id
                        && t.IsPrimary
                        && t.RevokedAt == null
                        && t.TitleDefinition!.Visibility == TitleVisibility.Public)
            .Select(t => t.TitleDefinition!.DisplayName)
            .FirstOrDefaultAsync(ct);

        Response.Headers.ETag = ETagHelper.Format(character.Version);

        return Ok(CharacterSummaryResponse.From(character, AuthController.PortraitUrl(character), primaryTitle));
    }
}

[ApiController]
[Route("api/v1/applications")]
[Authorize]
public sealed class ApplicationsController(
    CharacterApplicationService applications,
    GongWeiDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<ApplicationResponse>> GetMine(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var application = await db.CharacterApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (application is null)
        {
            return NoContent();
        }

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    [HttpPost]
    [RequireIdempotency]
    public async Task<ActionResult<ApplicationResponse>> Create(
        [FromBody] CreateApplicationRequest request,
        CancellationToken ct)
    {
        var application = await applications.CreateDraftAsync(ToInput(request), ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return CreatedAtAction(nameof(GetMine), null, ApplicationResponse.From(application));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> Update(
        Guid id,
        [FromBody] CreateApplicationRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var application = await applications.UpdateDraftAsync(id, version, ToInput(request), ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    [HttpPost("{id:guid}/submit")]
    [RequireIdempotency]
    public async Task<ActionResult<ApplicationResponse>> Submit(Guid id, CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var application = await applications.SubmitAsync(id, version, ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApplicationResponse>> Cancel(Guid id, CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var application = await applications.CancelAsync(id, version, ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(ApplicationResponse.From(application));
    }

    private static CreateApplicationInput ToInput(CreateApplicationRequest r) =>
        new(r.RequestedRole, r.CharacterName, r.FamilyName, r.Biography, r.Appearance,
            r.Personality, r.PresetPortraitId, r.PlayerPortraitSubmissionId, r.AnswersJson);
}

[ApiController]
[Route("api/v1/portraits")]
public sealed class PortraitsController(
    PortraitService portraits,
    GongWeiDbContext db,
    IMediaStorage storage,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>The official illustration set, filtered to a role.</summary>
    [HttpGet("presets")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PresetPortraitResponse>>> Presets(
        [FromQuery] string? role,
        CancellationToken ct = default)
    {
        var query = db.PresetPortraits.Where(p => p.IsActive);

        if (role is not null)
        {
            var parsed = EnumNaming.FromDbValue<CharacterRole>(role);
            query = query.Where(p => p.AppliesToRole == parsed);
        }

        var presets = await query
            .OrderBy(p => p.SortOrder)
            .Select(p => new PresetPortraitResponse(
                p.Id, p.Code, p.DisplayName,
                p.AppliesToRole == CharacterRole.Consort ? "consort"
                    : p.AppliesToRole == CharacterRole.Prince ? "prince" : "princess",
                p.ImageUrl, p.ThumbnailUrl))
            .ToListAsync(ct);

        return Ok(presets);
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<PortraitSubmissionResponse>>> Mine(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var submissions = await db.PlayerPortraitSubmissions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);

        return Ok(submissions.Select(PortraitSubmissionResponse.From).ToList());
    }

    /// <summary>
    /// Uploads one image. The 8 MB ceiling is enforced here, by ASP.NET Core's request
    /// size limit and by IIS requestFiltering — three layers, as the spec requires.
    /// </summary>
    [HttpPost]
    [Authorize]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<PortraitSubmissionResponse>> Upload(
        IFormFile file,
        [FromForm] CharacterRole appliesToRole,
        CancellationToken ct)
    {
        if (file.Length == 0)
        {
            throw DomainException.Validation(ErrorCodes.MediaDecodeFailed, "No file was uploaded.");
        }

        await using var stream = file.OpenReadStream();

        var submission = await portraits.UploadAsync(stream, file.FileName, appliesToRole, null, ct);

        Response.Headers.ETag = ETagHelper.Format(submission.Version);
        return Created($"/api/v1/portraits/{submission.Id}", PortraitSubmissionResponse.From(submission));
    }

    [HttpPatch("{id:guid}/crop")]
    [Authorize]
    public async Task<ActionResult<PortraitSubmissionResponse>> UpdateCrop(
        Guid id,
        [FromBody] CropRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);

        var submission = await portraits.UpdateCropAsync(
            id, version, new CropRect(request.X, request.Y, request.Width, request.Height), ct);

        Response.Headers.ETag = ETagHelper.Format(submission.Version);
        return Ok(PortraitSubmissionResponse.From(submission));
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize]
    public async Task<ActionResult<PortraitSubmissionResponse>> Withdraw(Guid id, CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var submission = await portraits.WithdrawAsync(id, version, ct);

        Response.Headers.ETag = ETagHelper.Format(submission.Version);
        return Ok(PortraitSubmissionResponse.From(submission));
    }

    /// <summary>
    /// Streams a portrait through a controlled endpoint. The media volume is never
    /// exposed as a browsable directory (spec §6.8 step 7).
    /// </summary>
    [HttpGet("/api/v1/media/portraits/{submissionId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Media(Guid submissionId, CancellationToken ct)
    {
        var storageKey = await portraits.ResolveServableStorageKeyAsync(submissionId, ct);
        var stream = await storage.OpenReadAsync(storageKey, ct);

        if (stream is null)
        {
            throw DomainException.NotFound("Portrait", submissionId);
        }

        Response.Headers.CacheControl = "private, max-age=300";
        return File(stream, "image/webp");
    }
}
