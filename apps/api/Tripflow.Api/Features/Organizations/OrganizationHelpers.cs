using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Tripflow.Api.Features.Organizations;

internal static class OrganizationHelpers
{
    internal static bool TryResolveOrganizationId(HttpContext context, out Guid organizationId, out IResult? error)
    {
        var role = context.User.FindFirstValue("role") ?? context.User.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            var header = context.Request.Headers["X-Org-Id"].FirstOrDefault();
            if (!Guid.TryParse(header, out organizationId))
            {
                error = Results.BadRequest(new { code = "invalid_org_scope", message = "X-Org-Id required for SuperAdmin." });
                return false;
            }

            error = null;
            return true;
        }

        var orgClaim = context.User.FindFirstValue("orgId");
        if (!Guid.TryParse(orgClaim, out organizationId))
        {
            error = Results.BadRequest(new { code = "invalid_org_scope", message = "orgId claim is required." });
            return false;
        }

        error = null;
        return true;
    }

    internal static void ApplyOrganizationContext(HttpContext context, Guid organizationId)
    {
        context.Request.Headers["X-Org-Id"] = organizationId.ToString();

        if (context.User.FindFirst("orgId")?.Value == organizationId.ToString())
        {
            return;
        }

        var claims = context.User.Claims
            .Where(x => !string.Equals(x.Type, "orgId", StringComparison.OrdinalIgnoreCase))
            .ToList();
        claims.Add(new Claim("orgId", organizationId.ToString()));

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: context.User.Identity?.AuthenticationType ?? "OrganizationContext",
            nameType: context.User.Identity is ClaimsIdentity currentIdentity ? currentIdentity.NameClaimType : ClaimTypes.Name,
            roleType: context.User.Identity is ClaimsIdentity currentRoleIdentity ? currentRoleIdentity.RoleClaimType : ClaimTypes.Role);

        context.User = new ClaimsPrincipal(identity);
    }
}
