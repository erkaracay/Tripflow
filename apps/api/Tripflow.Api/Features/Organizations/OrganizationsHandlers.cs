using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Organizations;

internal static class OrganizationsHandlers
{
    internal static async Task<IResult> GetOrganizations(bool? includeArchived, TripflowDbContext db, CancellationToken ct)
    {
        var showArchived = includeArchived ?? false;
        var orgs = await db.Organizations.AsNoTracking()
            .Where(x => showArchived || !x.IsDeleted)
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
            .Where(x => x.Id == orgId)
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
            RequireLast4ForQr = request.RequireLast4ForQr ?? false,
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
        var org = await db.Organizations.FirstOrDefaultAsync(x => x.Id == orgId, ct);
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

    internal static async Task<IResult> ArchiveOrganization(Guid orgId, TripflowDbContext db, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(x => x.Id == orgId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        if (!org.IsDeleted)
        {
            org.IsDeleted = true;
            org.IsActive = false;
            org.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

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

    internal static async Task<IResult> RestoreOrganization(Guid orgId, TripflowDbContext db, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(x => x.Id == orgId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        if (org.IsDeleted)
        {
            org.IsDeleted = false;
            org.IsActive = true;
            org.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

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

    internal static async Task<IResult> PurgeOrganization(Guid orgId, TripflowDbContext db, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orgId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        if (!org.IsDeleted)
        {
            return Results.Conflict(new { message = "Organization must be archived before purge." });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.CheckIns.RemoveRange(db.CheckIns.Where(x => x.OrganizationId == orgId));
        db.EventPortals.RemoveRange(db.EventPortals.Where(x => x.OrganizationId == orgId));
        db.PortalSessions.RemoveRange(db.PortalSessions.Where(x => x.OrganizationId == orgId));
        db.ParticipantAccesses.RemoveRange(db.ParticipantAccesses.Where(x => x.OrganizationId == orgId));
        db.ParticipantDetails.RemoveRange(db.ParticipantDetails.Where(x => x.Participant.OrganizationId == orgId));
        db.Participants.RemoveRange(db.Participants.Where(x => x.OrganizationId == orgId));
        db.Events.RemoveRange(db.Events.Where(x => x.OrganizationId == orgId));
        db.Users.RemoveRange(db.Users.Where(x => x.OrganizationId == orgId));
        db.Organizations.RemoveRange(db.Organizations.Where(x => x.Id == orgId));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.NoContent();
    }

    internal static Task<IResult> DeleteOrganization(Guid orgId, TripflowDbContext db, CancellationToken ct)
        => ArchiveOrganization(orgId, db, ct);

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
