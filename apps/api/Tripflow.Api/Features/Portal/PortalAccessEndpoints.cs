using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Portal;

public static class PortalAccessEndpoints
{
    public static IEndpointRouteBuilder MapPortalAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/access").WithTags("Portal");

        group.MapPost("/verify", PortalAccessHandlers.VerifyAccess)
            .WithSummary("Verify participant portal token")
            .WithDescription("Validates a participant portal token and returns verification requirements.")
            .AllowAnonymous();

        group.MapPost("/confirm", PortalAccessHandlers.ConfirmAccess)
            .WithSummary("Confirm participant portal access")
            .WithDescription("Validates last-4 phone digits and issues a portal session token.")
            .AllowAnonymous();

        group.MapGet("/me", PortalAccessHandlers.GetMe)
            .WithSummary("Portal session profile")
            .WithDescription("Returns participant data for a valid portal session.")
            .AllowAnonymous();

        return app;
    }
}
