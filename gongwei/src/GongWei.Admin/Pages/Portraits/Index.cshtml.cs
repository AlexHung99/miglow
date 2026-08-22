using GongWei.Admin.Security;
using GongWei.Application.Identity;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Portraits;

[Authorize(Policy = AdminPolicies.ReviewCharacters)]
public sealed class IndexModel(GongWeiDbContext db, PortraitService portraits) : PageModel
{
    public sealed record Row(
        Guid Id,
        string PlayerName,
        string Role,
        int WidthPx,
        int HeightPx,
        long ByteSize,
        DateTimeOffset SubmittedAt,
        bool IsReferencedByApplication,
        long Version);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var rows = await db.PlayerPortraitSubmissions
            .Where(s => s.ReviewStatus == PortraitReviewStatus.Pending)
            .OrderBy(s => s.SubmittedAt)
            .Select(s => new
            {
                s.Id,
                PlayerName = s.User!.DisplayName,
                s.AppliesToRole,
                s.MediaAsset!.WidthPx,
                s.MediaAsset.HeightPx,
                s.MediaAsset.ByteSize,
                s.SubmittedAt,
                IsReferenced = db.CharacterApplications.Any(a => a.PlayerPortraitSubmissionId == s.Id),
                s.Version
            })
            .Take(200)
            .ToListAsync(ct);

        Items = rows.Select(r => new Row(
            r.Id, r.PlayerName, EnumNaming.ToDbValue(r.AppliesToRole),
            r.WidthPx, r.HeightPx, r.ByteSize, r.SubmittedAt, r.IsReferenced, r.Version)).ToList();
    }

    public async Task<IActionResult> OnPostReviewAsync(
        Guid id,
        long version,
        bool approve,
        string? note,
        CancellationToken ct)
    {
        try
        {
            await portraits.ReviewAsync(id, version, approve, note, ct);
            TempData["Success"] = approve ? "圖片已通過審核。" : "圖片已駁回，相關申請已退回補件。";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }
}
