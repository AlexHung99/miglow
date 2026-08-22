using System.Security.Claims;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;

namespace GongWei.Api.Http;

/// <summary>
/// Claim types used on the session principal. Deliberately minimal: the LINE subject is
/// never a claim, so it cannot leak through a debug endpoint (spec §11).
/// </summary>
public static class GongWeiClaims
{
    public const string UserId = "gw:uid";
    public const string SessionId = "gw:sid";
    public const string AdminRole = "gw:role";
}

/// <summary>Reads the caller's identity off the ambient HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId =>
        Guid.TryParse(Context?.User.FindFirstValue(GongWeiClaims.UserId), out var id) ? id : null;

    public bool IsAuthenticated => UserId is not null;

    public IReadOnlySet<AdminRole> AdminRoles
    {
        get
        {
            var context = Context;

            if (context is null)
            {
                return new HashSet<AdminRole>();
            }

            var roles = new HashSet<AdminRole>();

            foreach (var claim in context.User.FindAll(GongWeiClaims.AdminRole))
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

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
        ? ua[..Math.Min(ua.Length, 512)]
        : null;

    public bool HasRole(AdminRole role) => AdminRoles.Contains(role);

    public Guid RequireUserId() =>
        UserId ?? throw new DomainException(
            ErrorCodes.AuthRequired,
            "Sign in first.",
            DomainErrorKind.Unauthenticated);

    public void RequireRole(params AdminRole[] anyOf)
    {
        RequireUserId();

        var held = AdminRoles;

        // SuperAdmin is a shortcut for holding the role, not an exemption from audit
        // or from the self-review rule (spec §9.1).
        if (held.Contains(Domain.Common.AdminRole.SuperAdmin) || anyOf.Any(held.Contains))
        {
            return;
        }

        throw new DomainException(
            ErrorCodes.Forbidden,
            $"This action needs one of: {string.Join(", ", anyOf.Select(EnumNaming.ToDbValue))}.",
            DomainErrorKind.Forbidden);
    }
}
