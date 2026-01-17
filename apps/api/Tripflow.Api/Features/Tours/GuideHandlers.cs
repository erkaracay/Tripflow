using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Tours;

internal static class GuideHandlers
{
    internal static async Task<IResult> GetTours(
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var tours = await db.Tours.AsNoTracking()
            .Where(x => x.GuideUserId == userId && x.OrganizationId == orgId)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new TourListItemDto(
                x.Id,
                x.Name,
                x.StartDate.ToString("yyyy-MM-dd"),
                x.EndDate.ToString("yyyy-MM-dd"),
                db.CheckIns.Count(c => c.TourId == x.Id),
                db.Participants.Count(p => p.TourId == x.Id),
                x.GuideUserId))
            .ToArrayAsync(ct);

        return Results.Ok(tours);
    }

    internal static async Task<IResult> GetParticipants(
        string tourId,
        string? query,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.TourId == id && x.OrganizationId == orgId);

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            participantsQuery = participantsQuery.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)));
        }

        var checkinsQuery = db.CheckIns.AsNoTracking()
            .Where(x => x.TourId == id && x.OrganizationId == orgId);

        var participants = await participantsQuery
            .OrderBy(x => x.FullName)
            .GroupJoin(
                checkinsQuery,
                participant => participant.Id,
                checkIn => checkIn.ParticipantId,
                (participant, checkIns) => new ParticipantDto(
                    participant.Id,
                    participant.FullName,
                    participant.Email,
                    participant.Phone,
                    participant.CheckInCode,
                    checkIns.Any()))
            .ToArrayAsync(ct);

        return Results.Ok(participants);
    }

    internal static async Task<IResult> GetCheckInSummary(
        string tourId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInSummary(arrivedCount, totalCount));
    }

    internal static async Task<IResult> CheckInByCode(
        string tourId,
        CheckInCodeRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        return await ToursHandlers.CheckInByCodeForOrg(orgId, tourId, request, db, ct);
    }

    internal static async Task<IResult> UndoCheckIn(
        string tourId,
        CheckInUndoRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        return await ToursHandlers.UndoCheckInForOrg(orgId, tourId, request, db, ct);
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId, out IResult? error)
    {
        var raw = user.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out userId))
        {
            error = Results.Unauthorized();
            return false;
        }

        error = null;
        return true;
    }
}
