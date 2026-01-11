using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;

namespace Tripflow.Api.Features.Users;

internal static class UsersHandlers
{
    internal static async Task<IResult> GetUsers(string? role, TripflowDbContext db, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();

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
