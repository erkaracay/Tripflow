using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Organizations;

public static class OrganizationsEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations")
            .WithTags("Organizations")
            .RequireAuthorization("SuperAdminOnly");

        group.MapGet("", OrganizationsHandlers.GetOrganizations)
            .WithSummary("List organizations")
            .WithDescription("Returns organizations for SuperAdmin org selection.")
            .Produces<OrganizationListItemDto[]>(StatusCodes.Status200OK);

        return app;
    }
}
