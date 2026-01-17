using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;

namespace Tripflow.Api.Features.Organizations;

internal static class OrganizationsHandlers
{
    internal static async Task<IResult> GetOrganizations(TripflowDbContext db, CancellationToken ct)
    {
        var orgs = await db.Organizations.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new OrganizationListItemDto(x.Id, x.Name, x.Slug))
            .ToArrayAsync(ct);

        return Results.Ok(orgs);
    }
}
