using System.Text.Json;
using GongWei.Admin.Security;
using GongWei.Application.Abstractions;
using GongWei.Application.Characters;
using GongWei.Application.Identity;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Applications;

[Authorize(Policy = AdminPolicies.ReviewCharacters)]
public sealed class ReviewModel(
    GongWeiDbContext db,
    CharacterApplicationService applications,
    PortraitService portraits,
    IMediaStorage mediaStorage) : PageModel
{
    public sealed record ApplicationView(
        Guid Id,
        string PlayerName,
        string FullName,
        string RoleLabel,
        string? CourtesyName,
        string AgeLabel,
        string StatusLabel,
        string Appearance,
        string Biography,
        string Personality,
        string Strengths,
        string Weaknesses,
        string Likes,
        string Dislikes,
        string FormData,
        string? PortraitUrl,
        bool HasUploadedPortrait,
        string PortraitKind,
        string PortraitName,
        string? PortraitReviewStatus,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        long Version,
        bool CanReview);

    public ApplicationView? Application { get; private set; }
    public IReadOnlyList<SelectListItem> Ranks { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Residences { get; private set; } = [];

    [BindProperty] public Guid? InitialRankId { get; set; }
    [BindProperty] public Guid? ResidenceId { get; set; }
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public long Version { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LoadAsync(id, ct)) return NotFound();
        Version = Application!.Version;
        return Page();
    }

    public async Task<IActionResult> OnGetPortraitAsync(Guid id, CancellationToken ct)
    {
        var mediaAssetId = await db.CharacterApplications
            .AsNoTracking()
            .Where(application => application.Id == id)
            .Select(application => application.PlayerPortraitSubmission == null
                ? (Guid?)null
                : application.PlayerPortraitSubmission.MediaAssetId)
            .FirstOrDefaultAsync(ct);

        if (mediaAssetId is null) return NotFound();

        var (storageKey, etag) = await portraits.ResolveServableAsync(mediaAssetId.Value, ct);
        var stream = await mediaStorage.OpenReadAsync(storageKey, ct);
        if (stream is null) return NotFound();

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=300";
        return File(stream, "image/webp");
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        if (InitialRankId is null)
        {
            TempData["Error"] = "核准前必須選擇初始位階。";
            await LoadAsync(id, ct);
            return Page();
        }

        try
        {
            await applications.ApproveAsync(new ApproveApplicationInput(
                id, Version, InitialRankId.Value, ResidenceId, null, Note), ct);
            TempData["Success"] = "已核准申請並建立正式角色。";
            return RedirectToPage("/Applications/Index");
        }
        catch (DomainException exception)
        {
            TempData["Error"] = exception.Message;
            await LoadAsync(id, ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRequestRevisionAsync(Guid id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Note))
        {
            TempData["Error"] = "退回補件必須填寫原因。";
            await LoadAsync(id, ct);
            return Page();
        }

        try
        {
            await applications.RequestRevisionAsync(id, Version, Note.Trim(), ct);
            TempData["Success"] = "已退回玩家補件。";
            return RedirectToPage("/Applications/Index");
        }
        catch (DomainException exception)
        {
            TempData["Error"] = exception.Message;
            await LoadAsync(id, ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Note))
        {
            TempData["Error"] = "拒絕申請必須填寫原因。";
            await LoadAsync(id, ct);
            return Page();
        }

        try
        {
            await applications.RejectAsync(id, Version, Note.Trim(), ct);
            TempData["Success"] = "已拒絕這筆申請。";
            return RedirectToPage("/Applications/Index");
        }
        catch (DomainException exception)
        {
            TempData["Error"] = exception.Message;
            await LoadAsync(id, ct);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var application = await db.CharacterApplications
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Portrait)
            .Include(item => item.PlayerPortraitSubmission)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (application is null) return false;

        var roleLabel = RoleLabel(application.Role);
        var portraitStatus = application.PlayerPortraitSubmission is null
            ? null
            : PortraitStatusLabel(application.PlayerPortraitSubmission.Status);

        Application = new ApplicationView(
            application.Id,
            application.User?.DisplayName ?? "（未知玩家）",
            application.FamilyName + application.GivenName,
            roleLabel,
            application.CourtesyName,
            application.Role == CharacterRole.Consort ? $"{application.Age} 歲" : "待生皇嗣",
            ApplicationStatusLabel(application.Status),
            application.Appearance,
            application.Biography,
            application.Personality,
            application.Strengths,
            application.Weaknesses,
            application.Likes,
            application.Dislikes,
            PrettyJson(application.FormData),
            application.Portrait?.AssetUrl,
            application.PlayerPortraitSubmissionId is not null,
            application.Portrait is null ? "自訂立繪" : "官方立繪",
            application.Portrait?.DisplayName ?? "玩家上傳圖片",
            portraitStatus,
            application.SubmittedAt,
            application.CreatedAt,
            application.UpdatedAt,
            application.Version,
            application.Status == ApplicationStatus.Submitted);

        Ranks = await db.Ranks
            .AsNoTracking()
            .Where(rank => rank.IsActive
                           && rank.IsApplicationOption
                           && rank.AppliesToRole == application.Role)
            .OrderBy(rank => rank.Ordinal)
            .ThenBy(rank => rank.DisplayName)
            .Select(rank => new SelectListItem(
                $"{rank.GradeCode}・{rank.DisplayName}（月俸 {rank.MonthlyStipend:N0}）",
                rank.Id.ToString()))
            .ToListAsync(ct);

        Residences = await db.Residences
            .AsNoTracking()
            .Where(residence => residence.IsActive)
            .OrderBy(residence => residence.DisplayName)
            .Select(residence => new SelectListItem(residence.DisplayName, residence.Id.ToString()))
            .ToListAsync(ct);

        return true;
    }

    private static string PrettyJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string RoleLabel(CharacterRole role) => role switch
    {
        CharacterRole.Consort => "嬪妃",
        CharacterRole.Prince => "皇子",
        CharacterRole.Princess => "帝姬",
        _ => EnumNaming.ToDbValue(role)
    };

    private static string ApplicationStatusLabel(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Draft => "草稿",
        ApplicationStatus.Submitted => "等待審核",
        ApplicationStatus.NeedsRevision => "等待補件",
        ApplicationStatus.Approved => "已核准",
        ApplicationStatus.Rejected => "已拒絕",
        ApplicationStatus.Cancelled => "已取消",
        _ => EnumNaming.ToDbValue(status)
    };

    private static string PortraitStatusLabel(PortraitSubmissionStatus status) => status switch
    {
        PortraitSubmissionStatus.Pending => "待審",
        PortraitSubmissionStatus.Approved => "已核准",
        PortraitSubmissionStatus.Rejected => "已拒絕",
        PortraitSubmissionStatus.Withdrawn => "已撤回",
        _ => EnumNaming.ToDbValue(status)
    };
}
