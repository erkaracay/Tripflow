using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Portal;

public static class PortalLoginEndpoints
{
    public static IEndpointRouteBuilder MapPortalLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").WithTags("Portal");

        group.MapPost("/login", PortalLoginHandlers.Login)
            .AllowAnonymous()
            .WithSummary("Portal login (event access code + TC)")
            .Produces<PortalLoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", PortalLoginHandlers.GetMe)
            .AllowAnonymous()
            .WithSummary("Portal session details")
            .WithDescription("Requires X-Portal-Session header or cookie.")
            .Produces<PortalMeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", PortalLoginHandlers.Logout)
            .AllowAnonymous()
            .WithSummary("Portal logout")
            .WithDescription("Clears the portal session cookie.")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/resolve", PortalLoginHandlers.ResolveEventAccessCode)
            .AllowAnonymous()
            .WithSummary("Resolve event access code")
            .WithDescription("Resolves eventId for a given event access code.")
            .Produces<PortalResolveEventResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
