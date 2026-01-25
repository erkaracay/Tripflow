using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Organizations;

internal static class OrganizationsHandlers
{
    internal static async Task<IResult> GetOrganizations(TripflowDbContext db, CancellationToken ct)
    {
        var orgs = await db.Organizations.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new OrganizationListItemDto(
                x.Id,
                x.Name,
                x.Slug,
                x.IsActive,
                x.IsDeleted,
                x.RequireLast4ForQr,
                x.RequireLast4ForPortal,
                x.CreatedAt,
                x.UpdatedAt))
            .ToArrayAsync(ct);

        return Results.Ok(orgs);
    }

    internal static async Task<IResult> GetOrganization(Guid orgId, TripflowDbContext db, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking()
            .Where(x => x.Id == orgId && !x.IsDeleted)
            .Select(x => new OrganizationDetailDto(
                x.Id,
                x.Name,
                x.Slug,
                x.IsActive,
                x.IsDeleted,
                x.RequireLast4ForQr,
                x.RequireLast4ForPortal,
                x.CreatedAt,
                x.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return org is null ? Results.NotFound() : Results.Ok(org);
    }

    internal static async Task<IResult> CreateOrganization(
        OrganizationCreateRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { message = "Name is required." });
        }

        var slug = Slugify(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Results.BadRequest(new { message = "Slug is required." });
        }

        var slugExists = await db.Organizations.AnyAsync(x => x.Slug == slug, ct);
        if (slugExists)
        {
            return Results.Conflict(new { message = "Slug already exists." });
        }

        var now = DateTime.UtcNow;
        var org = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true,
            IsDeleted = false,
            RequireLast4ForQr = request.RequireLast4ForQr ?? true,
            RequireLast4ForPortal = request.RequireLast4ForPortal ?? false,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        var dto = new OrganizationDetailDto(
            org.Id,
            org.Name,
            org.Slug,
            org.IsActive,
            org.IsDeleted,
            org.RequireLast4ForQr,
            org.RequireLast4ForPortal,
            org.CreatedAt,
            org.UpdatedAt);

        return Results.Created($"/api/organizations/{org.Id}", dto);
    }

    internal static async Task<IResult> UpdateOrganization(
        Guid orgId,
        OrganizationUpdateRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(x => x.Id == orgId && !x.IsDeleted, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { message = "Name is required." });
        }

        var slug = Slugify(request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Results.BadRequest(new { message = "Slug is required." });
        }

        var slugExists = await db.Organizations.AnyAsync(x => x.Slug == slug && x.Id != orgId, ct);
        if (slugExists)
        {
            return Results.Conflict(new { message = "Slug already exists." });
        }

        org.Name = name;
        org.Slug = slug;
        org.IsActive = request.IsActive;
        org.RequireLast4ForQr = request.RequireLast4ForQr;
        org.RequireLast4ForPortal = request.RequireLast4ForPortal;
        org.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var dto = new OrganizationDetailDto(
            org.Id,
            org.Name,
            org.Slug,
            org.IsActive,
            org.IsDeleted,
            org.RequireLast4ForQr,
            org.RequireLast4ForPortal,
            org.CreatedAt,
            org.UpdatedAt);

        return Results.Ok(dto);
    }

    internal static async Task<IResult> DeleteOrganization(Guid orgId, TripflowDbContext db, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(x => x.Id == orgId && !x.IsDeleted, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        var hasTours = await db.Tours.AnyAsync(x => x.OrganizationId == orgId, ct);
        if (hasTours)
        {
            return Results.BadRequest(new { message = "Org has active data." });
        }

        org.IsDeleted = true;
        org.IsActive = false;
        org.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static string Slugify(string input)
    {
        var trimmed = input.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[trimmed.Length];
        var idx = 0;
        var prevDash = false;

        foreach (var ch in trimmed)
        {
            var isLetter = ch is >= 'a' and <= 'z';
            var isDigit = ch is >= '0' and <= '9';
            if (isLetter || isDigit)
            {
                buffer[idx++] = ch;
                prevDash = false;
                continue;
            }

            if (!prevDash && idx > 0)
            {
                buffer[idx++] = '-';
                prevDash = true;
            }
        }

        if (idx > 0 && buffer[idx - 1] == '-')
        {
            idx--;
        }

        return new string(buffer[..idx]);
    }
}
