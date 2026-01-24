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

        group.MapGet("{orgId:guid}", OrganizationsHandlers.GetOrganization)
            .WithSummary("Get organization")
            .Produces<OrganizationDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", OrganizationsHandlers.CreateOrganization)
            .WithSummary("Create organization")
            .Produces<OrganizationDetailDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("{orgId:guid}", OrganizationsHandlers.UpdateOrganization)
            .WithSummary("Update organization")
            .Produces<OrganizationDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("{orgId:guid}", OrganizationsHandlers.DeleteOrganization)
            .WithSummary("Delete organization (soft)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
