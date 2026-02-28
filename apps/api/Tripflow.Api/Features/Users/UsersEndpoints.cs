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
            .WithDescription("Creates an Admin or creates/attaches a Guide with an explicit organizationId.")
            .Produces<UserUpsertResponseDto>(StatusCodes.Status201Created)
            .Produces<UserUpsertResponseDto>(StatusCodes.Status200OK)
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
            .WithSummary("Create or attach guide (Admin)")
            .WithDescription("Creates a Guide in the caller's organization or attaches an existing global guide account by email.")
            .Produces<UserUpsertResponseDto>(StatusCodes.Status201Created)
            .Produces<UserUpsertResponseDto>(StatusCodes.Status200OK)
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

        group.MapPost("/{userId:guid}/password", UsersHandlers.ChangePassword)
            .WithSummary("Change user password")
            .WithDescription("SuperAdmin can change any user password. Admin can change admin passwords within their organization, but guide passwords are managed globally.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
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
                                ["newPassword"] = new OpenApiString("StrongPass123!")
                            }
                        }
                    }
                };
                return op;
            });

        return app;
    }
}
