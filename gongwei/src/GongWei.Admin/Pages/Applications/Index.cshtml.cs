using GongWei.Admin.Security;
using GongWei.Application.Characters;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Applications;

[Authorize(Policy = AdminPolicies.ReviewCharacters)]
public sealed class IndexModel(GongWeiDbContext db, CharacterApplicationService applications) : PageModel
{
    public sealed record Row(
        Guid Id,
        string PlayerName,
        string CharacterName,
        string Role,
        DateTimeOffset? SubmittedAt,
        string? PortraitStatus,
        Guid? ClaimedBy,
        long Version);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var rows = await db.CharacterApplications
            .Where(a => a.Status == ApplicationStatus.Submitted)
            .OrderBy(a => a.SubmittedAt)
            .Select(a => new
            {
                a.Id,
                PlayerName = a.User!.DisplayName,
                a.CharacterName,
                a.RequestedRole,
                a.SubmittedAt,
                PortraitStatus = a.PlayerPortraitSubmission == null
                    ? (PortraitReviewStatus?)null
                    : a.PlayerPortraitSubmission.ReviewStatus,
                a.ClaimedBy,
                a.Version
            })
            .Take(200)
            .ToListAsync(ct);

        Items = rows.Select(r => new Row(
            r.Id,
            r.PlayerName,
            r.CharacterName,
            EnumNaming.ToDbValue(r.RequestedRole),
            r.SubmittedAt,
            r.PortraitStatus is null ? null : EnumNaming.ToDbValue(r.PortraitStatus.Value),
            r.ClaimedBy,
            r.Version)).ToList();
    }

    /// <summary>Claiming stops two reviewers working the same application in parallel.</summary>
    public async Task<IActionResult> OnPostClaimAsync(Guid id, long version, CancellationToken ct)
    {
        try
        {
            await applications.ClaimAsync(id, version, ct);
            TempData["Success"] = "已認領此申請。";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }
}
