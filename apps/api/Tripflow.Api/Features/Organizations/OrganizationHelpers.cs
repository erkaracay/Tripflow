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
                error = Results.BadRequest(new { message = "X-Org-Id required for SuperAdmin." });
                return false;
            }

            error = null;
            return true;
        }

        var orgClaim = context.User.FindFirstValue("orgId");
        if (!Guid.TryParse(orgClaim, out organizationId))
        {
            error = Results.BadRequest(new { message = "orgId claim is required." });
            return false;
        }

        error = null;
        return true;
    }
}
