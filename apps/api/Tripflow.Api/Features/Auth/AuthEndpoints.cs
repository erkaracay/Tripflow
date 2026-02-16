using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", AuthHandlers.Login)
            .AllowAnonymous()
            .WithSummary("Login")
            .WithDescription("Returns a JWT access token for admin/guide users and sets auth cookie.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", AuthHandlers.Logout)
            .AllowAnonymous()
            .WithSummary("Logout")
            .WithDescription("Clears the auth cookie.")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/me", AuthHandlers.GetMe)
            .RequireAuthorization()
            .WithSummary("Current user")
            .WithDescription("Returns current user from auth cookie. 401 if not authenticated.")
            .Produces<AuthMeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
