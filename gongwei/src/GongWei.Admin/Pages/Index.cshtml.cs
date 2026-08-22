using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages;

/// <summary>
/// The back office landing page.
///
/// Deliberately small: it reports what the system currently holds and which modules are
/// still to come. The v1.0-era review screens are quarantined in PendingV11 until task
/// #15 rewrites them, and a dashboard that linked to pages which do not compile would be
/// worse than one that says so.
/// </summary>
public class IndexModel(GongWeiDbContext db, ICurrentUser currentUser, IClock clock) : PageModel
{
    public string DisplayName { get; private set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; private set; } = [];

    public int UserCount { get; private set; }

    public int CharacterCount { get; private set; }

    public int OpenApplicationCount { get; private set; }

    public int RankCount { get; private set; }

    public int PublishedNpcCount { get; private set; }

    public bool RulesSeeded => RankCount > 0;

    public async Task OnGetAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        DisplayName = User.Identity?.Name ?? "（未知）";
        Roles = currentUser.AdminRoles.Select(EnumNaming.ToDbValue).OrderBy(r => r).ToList();

        UserCount = await db.Users.CountAsync(ct);
        CharacterCount = await db.Characters.CountAsync(ct);

        OpenApplicationCount = await db.CharacterApplications
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, ct);

        RankCount = await db.Ranks.CountAsync(ct);
        PublishedNpcCount = await db.Npcs.CountAsync(n => n.Status == NpcStatus.Published, ct);
    }
}
