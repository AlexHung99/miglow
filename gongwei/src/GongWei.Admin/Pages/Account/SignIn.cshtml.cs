using System.Security.Claims;
using GongWei.Admin.Security;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Account;

/// <summary>
/// Admin sign-in. Identity still comes from LINE Login, but the admin cookie is only
/// issued to an account that currently holds at least one admin role in the database —
/// roles are never carried over from the player session (spec §2.3).
/// </summary>
[AllowAnonymous]
public sealed class SignInModel(
    GongWeiDbContext db,
    ILineLoginClient line,
    IRandomProvider random,
    IClock clock,
    IAuditWriter audit) : PageModel
{
    private const string StateCookie = "gw_admin_oauth_state";
    private const string VerifierCookie = "gw_admin_oauth_verifier";

    public string? AuthorizeUrl { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet(string? error)
    {
        ErrorMessage = error;

        var state = random.NextUrlSafeToken(24);
        var nonce = random.NextUrlSafeToken(16);
        var verifier = random.NextUrlSafeToken(48);

        var challenge = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.ASCII.GetBytes(verifier)))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = clock.UtcNow.AddMinutes(10),
            Path = "/Account"
        };

        Response.Cookies.Append(StateCookie, state, options);
        Response.Cookies.Append(VerifierCookie, verifier, options);

        AuthorizeUrl = line.BuildAuthorizeUrl(state, nonce, challenge);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string code, string state, CancellationToken ct)
    {
        var expectedState = Request.Cookies[StateCookie];
        var verifier = Request.Cookies[VerifierCookie];

        if (string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(verifier) || expectedState != state)
        {
            return RedirectToPage("/Account/SignIn", new { error = "登入驗證失敗，請重新開始。" });
        }

        Response.Cookies.Delete(StateCookie);
        Response.Cookies.Delete(VerifierCookie);

        var profile = await line.ExchangeCodeAsync(code, verifier, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == profile.LineUserId, ct);

        if (user is null || !user.CanSignIn(clock.UtcNow))
        {
            return RedirectToPage("/Account/SignIn", new { error = "此帳號無法登入管理後台。" });
        }

        var roles = await db.AdminRoleAssignments
            .Where(a => a.UserId == user.Id && a.RevokedAt == null)
            .Select(a => a.Role)
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            return RedirectToPage("/Account/SignIn", new { error = "此帳號沒有任何管理權限。" });
        }

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(AdminClaims.UserId, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(AdminClaims.AdminRole, role.ToString()));
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        audit.Write("admin.sign_in", "user", user.Id,
            after: new { roles = roles.Select(EnumNaming.ToDbValue) });
        await db.SaveChangesAsync(ct);

        return RedirectToPage("/Index");
    }
}
