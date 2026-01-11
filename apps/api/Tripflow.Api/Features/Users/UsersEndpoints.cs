using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization("AdminOnly");

        group.MapGet("", UsersHandlers.GetUsers)
            .WithSummary("List users")
            .WithDescription("Lists users. Optional role filter, e.g. role=Guide.")
            .Produces<UserListItemDto[]>(StatusCodes.Status200OK);

        return app;
    }
}
