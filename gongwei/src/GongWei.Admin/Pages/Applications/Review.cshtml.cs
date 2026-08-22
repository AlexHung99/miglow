using GongWei.Admin.Security;
using GongWei.Application.Characters;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Applications;

[Authorize(Policy = AdminPolicies.ReviewCharacters)]
public sealed class ReviewModel(GongWeiDbContext db, CharacterApplicationService applications) : PageModel
{
    public sealed record ApplicationView(
        Guid Id,
        string PlayerName,
        string CharacterName,
        string? FamilyName,
        CharacterRole Role,
        string RoleLabel,
        string? Biography,
        string? Appearance,
        string? Personality,
        string? PortraitUrl,
        string? PortraitReviewStatus,
        long Version);

    public ApplicationView? Application { get; private set; }

    public IReadOnlyList<SelectListItem> Ranks { get; private set; } = [];

    public IReadOnlyList<SelectListItem> Residences { get; private set; } = [];

    /// <summary>Total ability points the reviewer may distribute (a published setting).</summary>
    public int AbilityBudget { get; private set; } = 120;

    [BindProperty]
    public Guid? RankId { get; set; }

    [BindProperty]
    public Guid? ResidenceId { get; set; }

    [BindProperty]
    public int Charm { get; set; }

    [BindProperty]
    public int Intellect { get; set; }

    [BindProperty]
    public int Artistry { get; set; }

    [BindProperty]
    public int Stamina { get; set; }

    [BindProperty]
    public string? Note { get; set; }

    [BindProperty]
    public long Version { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LoadAsync(id, ct))
        {
            return NotFound();
        }

        Version = Application!.Version;
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await applications.ApproveAsync(
                new ApproveApplicationInput(
                    id, Version, RankId, ResidenceId, Charm, Intellect, Artistry, Stamina, Note),
                ct);

            TempData["Success"] = "已核准並建立角色。";
            return RedirectToPage("/Applications/Index");
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            await LoadAsync(id, ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRequestRevisionAsync(Guid id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Note))
        {
            TempData["Error"] = "退回補件必須說明原因。";
            await LoadAsync(id, ct);
            return Page();
        }

        try
        {
            await applications.RequestRevisionAsync(id, Version, Note, ct);
            TempData["Success"] = "已退回補件。";
            return RedirectToPage("/Applications/Index");
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            await LoadAsync(id, ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Note))
        {
            TempData["Error"] = "駁回必須說明原因。";
            await LoadAsync(id, ct);
            return Page();
        }

        try
        {
            await applications.RejectAsync(id, Version, Note, ct);
            TempData["Success"] = "已駁回申請。";
            return RedirectToPage("/Applications/Index");
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            await LoadAsync(id, ct);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var application = await db.CharacterApplications
            .Include(a => a.User)
            .Include(a => a.PresetPortrait)
            .Include(a => a.PlayerPortraitSubmission)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (application is null)
        {
            return false;
        }

        Application = new ApplicationView(
            application.Id,
            application.User?.DisplayName ?? "(unknown)",
            application.CharacterName,
            application.FamilyName,
            application.RequestedRole,
            EnumNaming.ToDbValue(application.RequestedRole),
            application.Biography,
            application.Appearance,
            application.Personality,
            application.PresetPortrait?.ImageUrl
                ?? (application.PlayerPortraitSubmissionId is { } sid
                    ? $"/api/v1/media/portraits/{sid}"
                    : null),
            application.PlayerPortraitSubmission is { } submission
                ? EnumNaming.ToDbValue(submission.ReviewStatus)
                : null,
            application.Version);

        // Only offer ranks and residences that match the requested role — the approval
        // transaction rejects a mismatch anyway, but the reviewer should not see them.
        Ranks = await db.Ranks
            .Where(r => r.IsActive && r.AppliesToRole == application.RequestedRole)
            .OrderBy(r => r.Ordinal)
            .Select(r => new SelectListItem($"{r.DisplayName} ({r.Ordinal})", r.Id.ToString()))
            .ToListAsync(ct);

        Residences = await db.Residences
            .Where(r => r.IsActive
                        && (r.AppliesToRole == null || r.AppliesToRole == application.RequestedRole))
            .OrderBy(r => r.DisplayName)
            .Select(r => new SelectListItem(r.DisplayName, r.Id.ToString()))
            .ToListAsync(ct);

        var budget = await db.GameSettings
            .Where(s => s.Key == "character.initial_ability_points")
            .Select(s => s.PublishedValue)
            .FirstOrDefaultAsync(ct);

        if (int.TryParse(budget, out var parsed))
        {
            AbilityBudget = parsed;
        }

        return true;
    }
}
