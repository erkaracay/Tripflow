using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class GuideHandlers
{
    internal static async Task<IResult> GetEvents(
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

        var events = await db.Events.AsNoTracking()
            .Where(x => x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new EventListItemDto(
                x.Id,
                x.Name,
                x.StartDate.ToString("yyyy-MM-dd"),
                x.EndDate.ToString("yyyy-MM-dd"),
                db.CheckIns.Count(c => c.EventId == x.Id),
                db.Participants.Count(p => p.EventId == x.Id),
                x.GuideUserId,
                x.IsDeleted,
                x.EventAccessCode))
            .ToArrayAsync(ct);

        return Results.Ok(events);
    }

    internal static async Task<IResult> GetParticipants(
        string eventId,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId);

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
            .Where(x => x.EventId == id && x.OrganizationId == orgId);

        var logsQuery = db.EventParticipantLogs.AsNoTracking()
            .Where(x => x.OrganizationId == orgId && x.EventId == id && x.ParticipantId != null);

        var rows = await participantsQuery
            .OrderBy(x => x.FullName)
            .GroupJoin(
                checkinsQuery,
                participant => participant.Id,
                checkIn => checkIn.ParticipantId,
                (participant, checkIns) => new
                {
                    participant.Id,
                    participant.FullName,
                    participant.Phone,
                    participant.Email,
                    participant.TcNo,
                    participant.BirthDate,
                    participant.Gender,
                    participant.CheckInCode,
                    Arrived = checkIns.Any(),
                    LastLog = logsQuery
                        .Where(log => log.ParticipantId == participant.Id)
                        .OrderByDescending(log => log.CreatedAt)
                        .Select(log => new { log.Direction, log.Method, log.Result, log.CreatedAt })
                        .FirstOrDefault()
                })
            .ToListAsync(ct);

        var participants = rows.Select(row => new ParticipantDto(
                row.Id,
                row.FullName,
                row.Phone,
                row.Email,
                row.TcNo,
                row.BirthDate.ToString("yyyy-MM-dd"),
                row.Gender.ToString(),
                row.CheckInCode,
                row.Arrived,
                null,
                row.LastLog is null
                    ? null
                    : new ParticipantLastLogDto(
                        row.LastLog.Direction.ToString(),
                        row.LastLog.Method.ToString(),
                        row.LastLog.Result,
                        row.LastLog.CreatedAt)))
            .ToArray();

        return Results.Ok(participants);
    }

    internal static async Task<IResult> ResolveParticipantByCode(
        string eventId,
        string? code,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var normalized = NormalizeCheckInCode(code);
        if (normalized.Length != 8)
        {
            return Results.BadRequest(new { message = "Invalid code." });
        }

        var participant = await db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == id && x.OrganizationId == orgId && x.CheckInCode == normalized, ct);
        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.ParticipantId == participant.Id && x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new ParticipantResolveDto(
            participant.Id,
            participant.FullName,
            arrived,
            participant.CheckInCode));
    }

    internal static async Task<IResult> GetCheckInSummary(
        string eventId,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInSummary(arrivedCount, totalCount));
    }

    internal static async Task<IResult> GetSchedule(
        string eventId,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var schedule = await EventsHandlers.BuildScheduleAsync(id, orgId, db, ct);
        return Results.Ok(schedule);
    }

    internal static async Task<IResult> CheckInByCode(
        string eventId,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var actorRole = user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = null;
        }

        return await EventsHandlers.CheckInByCodeForOrg(orgId, eventId, request, userId, actorRole, ipAddress, userAgent, db, ct);
    }

    internal static async Task<IResult> UndoCheckIn(
        string eventId,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        return await EventsHandlers.UndoCheckInForOrg(orgId, eventId, request, db, ct);
    }

    internal static async Task<IResult> ResetAllCheckIns(
        string eventId,
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

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return parseError!;
        }

        var hasAccess = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.GuideUserId == userId && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        return await EventsHandlers.ResetAllCheckInsForOrg(orgId, eventId, db, ct);
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

    private static string NormalizeCheckInCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var cleaned = raw.Trim().ToUpperInvariant();
        var normalized = new string(cleaned.Where(char.IsLetterOrDigit).ToArray());
        return normalized;
    }
}
