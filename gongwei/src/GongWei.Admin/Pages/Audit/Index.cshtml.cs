using GongWei.Admin.Security;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Audit;

[Authorize(Policy = AdminPolicies.ReadAudit)]
public sealed class IndexModel(GongWeiDbContext db) : PageModel
{
    public sealed record Row(
        long Id,
        DateTimeOffset OccurredAt,
        string? ActorName,
        string? ActorRole,
        string Action,
        string? TargetType,
        Guid? TargetId,
        string? Reason,
        string? RequestId);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? ActionFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? BeforeId { get; set; }

    public long? NextCursor { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        const int PageSize = 100;

        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(ActionFilter))
        {
            query = query.Where(a => a.Action.StartsWith(ActionFilter));
        }

        if (BeforeId is { } cursor)
        {
            query = query.Where(a => a.Id < cursor);
        }

        var rows = await query
            .OrderByDescending(a => a.Id)
            .Take(PageSize)
            .Select(a => new Row(
                a.Id,
                a.OccurredAt,
                db.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.DisplayName).FirstOrDefault(),
                a.ActorRole,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Reason,
                a.RequestId))
            .ToListAsync(ct);

        Items = rows;
        NextCursor = rows.Count == PageSize ? rows[^1].Id : null;
    }
}
