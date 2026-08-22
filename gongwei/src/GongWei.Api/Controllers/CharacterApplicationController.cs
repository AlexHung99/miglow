using GongWei.Api.Contracts;
using GongWei.Api.Http;
using GongWei.Application.Abstractions;
using GongWei.Application.Characters;
using GongWei.Application.Identity;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Controllers;

/// <summary>
/// Build-a-character: the player's own side of the flow (api_v1_v1.1 §3.1).
///
/// A draft may be saved incomplete as often as the player likes; the full length and age
/// rules only run on submit. That split is why the request DTO validates almost nothing
/// and <see cref="CharacterApplication.EnsureReadyForSubmission"/> validates everything.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class CharacterApplicationController(
    CharacterApplicationService applications,
    PortraitService portraits,
    GongWeiDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// The application still in play, or 204 when there is none.
    ///
    /// 204 rather than 404: "you have no open application" is a normal state for most
    /// players, and a 404 would push the front end into treating it as an error.
    /// </summary>
    [HttpGet("character-applications/current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var application = await db.CharacterApplications
            .AsNoTracking()
            .Where(a => a.UserId == userId
                        && (a.Status == ApplicationStatus.Draft
                            || a.Status == ApplicationStatus.Submitted
                            || a.Status == ApplicationStatus.NeedsRevision))
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (application is null)
        {
            return NoContent();
        }

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(CharacterApplicationResponse.From(application));
    }

    /// <summary>Creates the draft. Rejected when a live character or another open application exists.</summary>
    [HttpPost("character-applications")]
    [RequireIdempotency]
    public async Task<ActionResult<CharacterApplicationResponse>> Create(
        [FromBody] ApplicationFormRequest request,
        CancellationToken ct)
    {
        var application = await applications.CreateDraftAsync(request.ToInput(), ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);

        return CreatedAtAction(
            nameof(Current),
            null,
            CharacterApplicationResponse.From(application));
    }

    /// <summary>
    /// Saves the draft. <c>If-Match</c> is required so two tabs editing the same form
    /// cannot silently overwrite one another.
    /// </summary>
    [HttpPatch("character-applications/{id:guid}")]
    public async Task<ActionResult<CharacterApplicationResponse>> Update(
        Guid id,
        [FromBody] ApplicationFormRequest request,
        CancellationToken ct)
    {
        var expectedVersion = ETagHelper.RequireIfMatch(Request);
        var application = await applications.UpdateDraftAsync(id, expectedVersion, request.ToInput(), ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(CharacterApplicationResponse.From(application));
    }

    /// <summary>
    /// Submits for review. This is where every length and age rule runs, and where the
    /// revision snapshot is written.
    ///
    /// The version may arrive in the body or in <c>If-Match</c>; the body wins because
    /// this is a POST and some clients will not attach If-Match to one.
    /// </summary>
    [HttpPost("character-applications/{id:guid}/submit")]
    [RequireIdempotency]
    public async Task<ActionResult<CharacterApplicationResponse>> Submit(
        Guid id,
        [FromBody] SubmitApplicationRequest? request,
        CancellationToken ct)
    {
        var expectedVersion = request?.ExpectedVersion ?? ETagHelper.RequireIfMatch(Request);
        var application = await applications.SubmitAsync(id, expectedVersion, ct);

        Response.Headers.ETag = ETagHelper.Format(application.Version);
        return Ok(CharacterApplicationResponse.From(application));
    }

    /// <summary>The official illustration set, filtered to the role being applied for.</summary>
    [HttpGet("portraits")]
    public async Task<ActionResult<IReadOnlyList<PortraitSummaryResponse>>> Portraits(
        [FromQuery] string? role,
        CancellationToken ct)
    {
        var query = db.PresetPortraits.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!EnumNaming.TryParse<CharacterRole>(role, out var parsed))
            {
                throw DomainException.FieldErrors(new Dictionary<string, string[]>
                {
                    ["role"] = ["角色類型必須是 consort、prince 或 princess。"]
                });
            }

            query = query.Where(p => p.Role == parsed);
        }

        var results = await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        return Ok(results.Select(PortraitSummaryResponse.From).ToList());
    }

    /// <summary>
    /// Uploads a player-drawn portrait. The file is decoded, stripped of metadata and
    /// re-encoded before anything is stored, so what lands on disk is never the bytes
    /// that arrived (§6.8).
    /// </summary>
    [HttpPost("portrait-uploads")]
    [RequireIdempotency]
    [RequestSizeLimit(MediaAsset.MaxByteSize)]
    public async Task<ActionResult<PortraitUploadResponse>> UploadPortrait(
        [FromForm] IFormFile file,
        [FromForm] string role,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["file"] = ["請選擇要上傳的圖片。"]
            });
        }

        if (!EnumNaming.TryParse<CharacterRole>(role, out var parsed))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["role"] = ["角色類型必須是 consort、prince 或 princess。"]
            });
        }

        await using var stream = file.OpenReadStream();

        // The declared content type is passed through for the audit trail only — the
        // processor decides what the file actually is from its magic bytes.
        var submission = await portraits.UploadAsync(
            stream, file.FileName, file.ContentType, parsed, ct);

        var withAsset = await db.PlayerPortraitSubmissions
            .AsNoTracking()
            .Include(s => s.MediaAsset)
            .FirstAsync(s => s.Id == submission.Id, ct);

        Response.Headers.ETag = ETagHelper.Format(withAsset.Version);

        return CreatedAtAction(
            nameof(UploadPortrait),
            new { id = withAsset.Id },
            PortraitUploadResponse.From(withAsset));
    }
}
