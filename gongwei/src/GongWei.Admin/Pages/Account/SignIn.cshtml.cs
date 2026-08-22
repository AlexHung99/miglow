using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GongWei.Admin.Security;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Admin.Pages.Account;

/// <summary>
/// Local username and password sign-in for the control back office.
///
/// A departure from backend_spec_v1.1 §155, where admin identity also arrives through
/// LINE Login. Three things keep the extra door narrow: the account is locked for fifteen
/// minutes after five failures, every attempt is audited whether it succeeds or not, and
/// the same message comes back for a wrong username, a wrong password and a locked
/// account — so this page cannot be used to discover which admin accounts exist.
/// </summary>
[AllowAnonymous]
public class SignInModel(
    GongWeiDbContext db,
    IPasswordHasher passwords,
    IClock clock,
    IAuditWriter audit,
    ILogger<SignInModel> logger) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "請輸入帳號。")]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "請輸入密碼。")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = clock.UtcNow;
        var username = Username.Trim();

        var credential = await db.AdminCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Username.ToLower() == username.ToLower(), ct);

        if (credential is null)
        {
            // Hash anyway. Returning immediately would make a missing account measurably
            // faster than a wrong password, which is enough to enumerate admins.
            passwords.Verify(Password, DummyHash);

            return await FailAsync(null, "unknown_username", ct);
        }

        if (credential.IsLockedAt(now))
        {
            return await FailAsync(credential, "locked", ct);
        }

        if (credential.User is null || credential.User.Status != UserStatus.Active)
        {
            return await FailAsync(credential, "account_inactive", ct);
        }

        var verification = passwords.Verify(Password, credential.PasswordHash);

        if (verification == PasswordVerification.Failed)
        {
            credential.RegisterFailure(now);
            return await FailAsync(credential, "bad_password", ct);
        }

        // Correct, but hashed with parameters weaker than today's default. Re-hash now,
        // while the plaintext is in hand — there is no other moment when it can be done.
        if (verification == PasswordVerification.SucceededNeedsRehash)
        {
            credential.PasswordHash = passwords.Hash(Password);
            credential.PasswordChangedAt = now;
            logger.LogInformation("Upgraded the password hash parameters for an admin account.");
        }

        var roles = await db.AdminRoleAssignments
            .Where(a => a.UserId == credential.UserId && (a.ExpiresAt == null || a.ExpiresAt > now))
            .Select(a => a.Role)
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            // Credentials are not authority. An account whose roles have all lapsed gets
            // no cookie, because the fallback policy would otherwise let it reach pages
            // that only check "is authenticated".
            return await FailAsync(credential, "no_admin_role", ct);
        }

        credential.RegisterSuccess(now);
        credential.User.LastLoginAt = now;
        credential.User.LastSeenAt = now;

        audit.Write(
            action: "admin.local_login",
            targetType: "user",
            targetId: credential.UserId,
            after: new { result = "success", username = credential.Username });

        await db.SaveChangesAsync(ct);

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(AdminClaims.UserId, credential.UserId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, credential.User.DisplayName));

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(AdminClaims.AdminRole, EnumNaming.ToDbValue(role)));
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        logger.LogInformation("Admin signed in locally.");

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    /// <summary>
    /// One message for every failure. Distinguishing "no such account" from "wrong
    /// password" would turn this form into a way to enumerate admin usernames, and
    /// naming the lockout would tell an attacker their guessing is working.
    /// </summary>
    private async Task<IActionResult> FailAsync(
        AdminCredential? credential,
        string reason,
        CancellationToken ct)
    {
        // The reason is recorded for the operator, never shown to the caller.
        audit.Write(
            action: "admin.local_login.failed",
            targetType: credential is null ? null : "user",
            targetId: credential?.UserId,
            after: new { result = "failed", reason },
            reason: reason);

        await db.SaveChangesAsync(ct);

        logger.LogWarning("Local admin sign-in failed: {Reason}", reason);

        ErrorMessage = "帳號或密碼不正確。";
        return Page();
    }

    /// <summary>
    /// Only same-site paths. <see cref="Url.IsLocalUrl"/> rejects absolute URLs and
    /// protocol-relative ones, which is what stops this being an open redirect.
    /// </summary>
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    /// <summary>
    /// A structurally valid hash of a value nobody knows, used to spend the same time on
    /// an unknown username as on a wrong password.
    /// </summary>
    private const string DummyHash =
        "pbkdf2-sha256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
}
