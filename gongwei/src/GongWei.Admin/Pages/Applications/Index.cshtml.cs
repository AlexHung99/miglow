using GongWei.Admin.Security;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Applications;

[Authorize(Policy = AdminPolicies.ReviewCharacters)]
public sealed class IndexModel(GongWeiDbContext db) : PageModel
{
    public sealed record Row(
        Guid Id,
        string PlayerName,
        string FullName,
        string RoleLabel,
        string PortraitLabel,
        DateTimeOffset SubmittedAt,
        long Version);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var rows = await db.CharacterApplications
            .AsNoTracking()
            .Where(application => application.Status == ApplicationStatus.Submitted)
            .Include(application => application.User)
            .Include(application => application.Portrait)
            .Include(application => application.PlayerPortraitSubmission)
            .OrderBy(application => application.SubmittedAt)
            .Take(200)
            .ToListAsync(ct);

        Items = rows.Select(application => new Row(
            application.Id,
            application.User?.DisplayName ?? "（未知玩家）",
            application.FamilyName + application.GivenName,
            RoleLabel(application.Role),
            application.Portrait?.DisplayName
                ?? (application.PlayerPortraitSubmission is null
                    ? "未選擇"
                    : $"自訂立繪・{StatusLabel(application.PlayerPortraitSubmission.Status)}"),
            application.SubmittedAt ?? application.UpdatedAt,
            application.Version)).ToList();
    }

    private static string RoleLabel(CharacterRole role) => role switch
    {
        CharacterRole.Consort => "嬪妃",
        CharacterRole.Prince => "皇子",
        CharacterRole.Princess => "帝姬",
        _ => EnumNaming.ToDbValue(role)
    };

    private static string StatusLabel(PortraitSubmissionStatus status) => status switch
    {
        PortraitSubmissionStatus.Pending => "待審",
        PortraitSubmissionStatus.Approved => "已核准",
        PortraitSubmissionStatus.Rejected => "已拒絕",
        PortraitSubmissionStatus.Withdrawn => "已撤回",
        _ => EnumNaming.ToDbValue(status)
    };
}
