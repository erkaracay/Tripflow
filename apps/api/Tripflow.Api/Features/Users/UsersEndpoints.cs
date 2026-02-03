using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

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
            .WithDescription("Lists users. Optional role filter, e.g. role=Guide. SuperAdmin may pass orgId.")
            .Produces<UserListItemDto[]>(StatusCodes.Status200OK);

        group.MapPost("", UsersHandlers.CreateUser)
            .WithSummary("Create user (SuperAdmin)")
            .WithDescription("Creates an Admin or Guide with an explicit organizationId.")
            .Produces<UserListItemDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization("SuperAdminOnly")
            .WithOpenApi(op =>
            {
                op.RequestBody = new OpenApiRequestBody
                {
                    Content =
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiObject
                            {
                                ["email"] = new OpenApiString("adminC@demo.local"),
                                ["password"] = new OpenApiString("TempPass123!"),
                                ["role"] = new OpenApiString("Admin"),
                                ["organizationId"] = new OpenApiString("00000000-0000-0000-0000-000000000000"),
                                ["fullName"] = new OpenApiString("Org C Admin")
                            }
                        }
                    }
                };
                return op;
            });

        group.MapPost("/guides", UsersHandlers.CreateGuide)
            .WithSummary("Create guide (Admin)")
            .WithDescription("Creates a Guide in the caller's organization.")
            .Produces<UserListItemDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithOpenApi(op =>
            {
                op.RequestBody = new OpenApiRequestBody
                {
                    Content =
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiObject
                            {
                                ["email"] = new OpenApiString("guideC@demo.local"),
                                ["password"] = new OpenApiString("TempPass123!"),
                                ["fullName"] = new OpenApiString("Org C Guide")
                            }
                        }
                    }
                };
                return op;
            });

        return app;
    }
}
