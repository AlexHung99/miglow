using System.Security.Claims;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace GongWei.Admin.Security;

public static class AdminPolicies
{
    public const string ReviewCharacters = "review-characters";
    public const string ManageGameplay = "manage-gameplay";
    public const string ManageEconomy = "manage-economy";
    public const string ReadAudit = "read-audit";
    public const string ReviewApprovals = "review-approvals";
}

public static class AdminClaims
{
    public const string UserId = "gw:uid";
    public const string AdminRole = "gw:role";
}

public static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>
    /// Requires one of the given roles. Roles come from the database on every sign-in;
    /// a removed role means the next sign-in has it gone, and revoking the session kills
    /// the current one immediately (spec §11).
    /// </summary>
    public static AuthorizationPolicyBuilder RequireAdminRole(
        this AuthorizationPolicyBuilder builder,
        params AdminRole[] anyOf)
    {
        var names = anyOf.Select(r => r.ToString()).ToHashSet(StringComparer.Ordinal);

        return builder.RequireAssertion(context =>
            context.User.FindAll(AdminClaims.AdminRole).Any(c => names.Contains(c.Value)));
    }
}

/// <summary>
/// The admin site's view of the caller. Shares the Application layer's
/// <see cref="ICurrentUser"/> so use cases enforce exactly the same rules whether they
/// are reached from the API or from an admin form (spec §2.2).
/// </summary>
public sealed class AdminCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId =>
        Guid.TryParse(Context?.User.FindFirstValue(AdminClaims.UserId), out var id) ? id : null;

    public bool IsAuthenticated => UserId is not null;

    public IReadOnlySet<AdminRole> AdminRoles
    {
        get
        {
            var roles = new HashSet<AdminRole>();

            if (Context is null)
            {
                return roles;
            }

            foreach (var claim in Context.User.FindAll(AdminClaims.AdminRole))
            {
                if (Enum.TryParse<AdminRole>(claim.Value, out var role))
                {
                    roles.Add(role);
                }
            }

            return roles;
        }
    }

    public string? RequestId => Context?.TraceIdentifier;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();

    public bool HasRole(AdminRole role) => AdminRoles.Contains(role);

    public Guid RequireUserId() =>
        UserId ?? throw new DomainException(
            ErrorCodes.AuthRequired, "Sign in first.", DomainErrorKind.Unauthenticated);

    public void RequireRole(params AdminRole[] anyOf)
    {
        RequireUserId();

        var held = AdminRoles;

        if (held.Contains(AdminRole.SuperAdmin) || anyOf.Any(held.Contains))
        {
            return;
        }

        throw new DomainException(
            ErrorCodes.Forbidden,
            $"This action needs one of: {string.Join(", ", anyOf.Select(EnumNaming.ToDbValue))}.",
            DomainErrorKind.Forbidden);
    }
}
