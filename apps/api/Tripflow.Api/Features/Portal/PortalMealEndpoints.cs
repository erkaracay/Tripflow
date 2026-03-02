using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Portal;

public static class PortalMealEndpoints
{
    public static IEndpointRouteBuilder MapPortalMealEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").WithTags("Portal");

        group.MapGet("/activities/{activityId}/meal", PortalMealHandlers.GetMeal)
            .AllowAnonymous()
            .WithSummary("Portal meal groups and current selections")
            .WithDescription("Requires X-Portal-Session header or portal session cookie.")
            .Produces<PortalMealResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/activities/{activityId}/meal", PortalMealHandlers.UpsertMealSelections)
            .AllowAnonymous()
            .WithSummary("Portal save meal selections")
            .WithDescription("Requires X-Portal-Session header or portal session cookie.")
            .Produces<PortalMealResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
