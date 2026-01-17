using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Users;

internal static class UsersHandlers
{
    internal static async Task<IResult> GetUsers(string? role, HttpContext httpContext, TripflowDbContext db, CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var error))
        {
            return error!;
        }

        var query = db.Users.AsNoTracking()
            .Where(x => x.OrganizationId == orgId);

        var roleFilter = role?.Trim();
        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            query = query.Where(x => x.Role.ToLower() == roleFilter.ToLower());
        }

        var users = await query
            .OrderBy(x => x.FullName ?? x.Email)
            .Select(x => new UserListItemDto(x.Id, x.Email, x.FullName, x.Role))
            .ToArrayAsync(ct);

        return Results.Ok(users);
    }
}
