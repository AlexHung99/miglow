using GongWei.Admin.Security;
using GongWei.Application.Abstractions;
using GongWei.Application.Operations;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Approvals;

[Authorize(Policy = AdminPolicies.ReviewApprovals)]
public sealed class IndexModel(
    GongWeiDbContext db,
    ApprovalService approvals,
    ICurrentUser currentUser) : PageModel
{
    public sealed record Row(
        Guid Id,
        string Handler,
        string Status,
        string Reason,
        string? TargetType,
        Guid? TargetId,
        string RequestedByName,
        bool RequestedByMe,
        DateTimeOffset RequestedAt,
        DateTimeOffset ExpiresAt,
        bool IsExpired,
        long Version);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        var now = DateTimeOffset.UtcNow;

        var rows = await db.ApprovalRequests
            .Where(r => r.Status == ApprovalStatus.Pending || r.Status == ApprovalStatus.Approved)
            .OrderBy(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                r.ActionHandler,
                r.Status,
                r.Reason,
                r.TargetType,
                r.TargetId,
                RequestedByName = db.Users
                    .Where(u => u.Id == r.RequestedBy)
                    .Select(u => u.DisplayName)
                    .FirstOrDefault() ?? "(unknown)",
                r.RequestedBy,
                r.RequestedAt,
                r.ExpiresAt,
                r.Version
            })
            .Take(200)
            .ToListAsync(ct);

        Items = rows.Select(r => new Row(
            r.Id,
            EnumNaming.ToDbValue(r.ActionHandler),
            EnumNaming.ToDbValue(r.Status),
            r.Reason,
            r.TargetType,
            r.TargetId,
            r.RequestedByName,
            r.RequestedBy == me,
            r.RequestedAt,
            r.ExpiresAt,
            r.ExpiresAt <= now,
            r.Version)).ToList();
    }

    public async Task<IActionResult> OnPostDecideAsync(
        Guid id,
        long version,
        bool approve,
        string? note,
        CancellationToken ct)
    {
        try
        {
            await approvals.DecideAsync(
                id, version,
                approve ? ApprovalDecisionKind.Approve : ApprovalDecisionKind.Reject,
                note, ct);

            TempData["Success"] = approve ? "已核可，可交由第三方執行。" : "已否決。";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExecuteAsync(Guid id, long version, CancellationToken ct)
    {
        try
        {
            await approvals.ExecuteAsync(id, version, ct);
            TempData["Success"] = "已執行。";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }
}
