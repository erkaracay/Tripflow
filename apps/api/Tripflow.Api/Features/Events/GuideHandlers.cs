using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
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
            .Include(x => x.EventGuides)
            .Where(x => x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new EventListItemDto(
                x.Id,
                x.Name,
                x.StartDate.ToString("yyyy-MM-dd"),
                x.EndDate.ToString("yyyy-MM-dd"),
                db.CheckIns.Count(c => c.EventId == x.Id),
                db.Participants.Count(p => p.EventId == x.Id),
                x.EventGuides.Select(g => g.GuideUserId).ToArray(),
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var participantsQuery = db.Participants.AsNoTracking()
            .Include(x => x.Details)
            .Where(x => x.EventId == id && x.OrganizationId == orgId);

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            participantsQuery = participantsQuery.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || EF.Functions.ILike(x.FirstName, pattern)
                || EF.Functions.ILike(x.LastName, pattern)
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)));
        }

        var checkinsQuery = db.CheckIns.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId);

        var logsQuery = db.EventParticipantLogs.AsNoTracking()
            .Where(x => x.OrganizationId == orgId
                        && x.EventId == id
                        && x.ParticipantId != null
                        && (x.Result == "Success" || x.Result == "AlreadyArrived"));

        var rows = await participantsQuery
            .OrderBy(x => x.FullName)
            .GroupJoin(
                checkinsQuery,
                participant => participant.Id,
                checkIn => checkIn.ParticipantId,
                (participant, checkIns) => new
                {
                    participant.Id,
                    participant.FirstName,
                    participant.LastName,
                    participant.FullName,
                    participant.Phone,
                    participant.Email,
                    participant.TcNo,
                    participant.BirthDate,
                    participant.Gender,
                    participant.CheckInCode,
                    participant.WillNotAttend,
                    Arrived = checkIns.Any(),
                    Details = MapDetails(participant.Details),
                    LastLog = logsQuery
                        .Where(log => log.ParticipantId == participant.Id)
                        .OrderByDescending(log => log.CreatedAt)
                        .Select(log => new { log.Direction, log.Method, log.Result, log.CreatedAt })
                        .FirstOrDefault()
                })
            .ToListAsync(ct);

        var participants = rows.Select(row => new ParticipantDto(
                row.Id,
                row.FirstName,
                row.LastName,
                row.FullName,
                row.Phone,
                row.Email,
                row.TcNo,
                row.BirthDate.ToString("yyyy-MM-dd"),
                row.Gender.ToString(),
                row.CheckInCode,
                row.Arrived,
                row.WillNotAttend,
                row.Details,
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

    internal static async Task<IResult> SetParticipantWillNotAttend(
        string eventId,
        string participantId,
        ParticipantWillNotAttendRequest request,
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

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        if (request is null || !request.WillNotAttend.HasValue)
        {
            return EventsHelpers.BadRequest("willNotAttend is required.");
        }

        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var participant = await db.Participants
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        participant.WillNotAttend = request.WillNotAttend.Value;
        await db.SaveChangesAsync(ct);

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.ParticipantId == participant.Id && x.OrganizationId == orgId, ct);

        var lastLog = await db.EventParticipantLogs.AsNoTracking()
            .Where(x => x.OrganizationId == orgId
                        && x.EventId == id
                        && x.ParticipantId == participant.Id
                        && (x.Result == "Success" || x.Result == "AlreadyArrived"))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ParticipantLastLogDto(
                x.Direction.ToString(),
                x.Method.ToString(),
                x.Result,
                x.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new ParticipantWillNotAttendResponseDto(
            participant.Id,
            participant.WillNotAttend,
            arrived,
            lastLog));
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
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
            participant.FirstName,
            participant.LastName,
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        var arrivedCount = await db.CheckIns.AsNoTracking()
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
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
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
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

    private static ParticipantDetailsDto? MapDetails(ParticipantDetailsEntity? details)
    {
        if (details is null)
        {
            return null;
        }

        return new ParticipantDetailsDto(
            details.RoomNo,
            details.RoomType,
            details.BoardType,
            details.PersonNo,
            details.AgencyName,
            details.City,
            details.FlightCity,
            details.HotelCheckInDate?.ToString("yyyy-MM-dd"),
            details.HotelCheckOutDate?.ToString("yyyy-MM-dd"),
            details.ArrivalTicketNo ?? details.TicketNo,
            details.ArrivalTicketNo ?? details.TicketNo,
            details.ReturnTicketNo,
            details.AttendanceStatus,
            details.InsuranceCompanyName,
            details.InsurancePolicyNo,
            details.InsuranceStartDate?.ToString("yyyy-MM-dd"),
            details.InsuranceEndDate?.ToString("yyyy-MM-dd"),
            details.ArrivalAirline,
            details.ArrivalDepartureAirport,
            details.ArrivalArrivalAirport,
            details.ArrivalFlightCode,
            details.ArrivalFlightDate?.ToString("yyyy-MM-dd"),
            details.ArrivalDepartureTime?.ToString("HH:mm"),
            details.ArrivalArrivalTime?.ToString("HH:mm"),
            details.ArrivalPnr,
            details.ArrivalBaggageAllowance,
            details.ArrivalBaggagePieces,
            details.ArrivalBaggageTotalKg,
            details.ArrivalCabinBaggage,
            details.ReturnAirline,
            details.ReturnDepartureAirport,
            details.ReturnArrivalAirport,
            details.ReturnFlightCode,
            details.ReturnFlightDate?.ToString("yyyy-MM-dd"),
            details.ReturnDepartureTime?.ToString("HH:mm"),
            details.ReturnArrivalTime?.ToString("HH:mm"),
            details.ReturnPnr,
            details.ReturnBaggageAllowance,
            details.ReturnBaggagePieces,
            details.ReturnBaggageTotalKg,
            details.ReturnCabinBaggage,
            details.ArrivalTransferPickupTime?.ToString("HH:mm"),
            details.ArrivalTransferPickupPlace,
            details.ArrivalTransferDropoffPlace,
            details.ArrivalTransferVehicle,
            details.ArrivalTransferPlate,
            details.ArrivalTransferDriverInfo,
            details.ArrivalTransferNote,
            details.ReturnTransferPickupTime?.ToString("HH:mm"),
            details.ReturnTransferPickupPlace,
            details.ReturnTransferDropoffPlace,
            details.ReturnTransferVehicle,
            details.ReturnTransferPlate,
            details.ReturnTransferDriverInfo,
            details.ReturnTransferNote);
    }

    internal static async Task<IResult> GetActivitiesForCheckIn(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventsHandlers.GetActivitiesForCheckIn(eventId, httpContext, db, ct);
    }

    internal static async Task<IResult> PostActivityCheckIn(
        string eventId,
        string activityId,
        ActivityCheckInRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await ActivityCheckInHandlers.PostCheckIn(eventId, activityId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> GetActivityParticipantsTable(
        string eventId,
        string activityId,
        string? query,
        string? status,
        int? page,
        int? pageSize,
        string? sort,
        string? dir,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await ActivityCheckInHandlers.GetParticipantsTable(eventId, activityId, query, status, page, pageSize, sort, dir, httpContext, db, ct);
    }

    internal static async Task<IResult> ResetAllActivityCheckIns(
        string eventId,
        string activityId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
            return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
            return parseError!;

        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
            return Results.NotFound(new { message = "Event not found." });

        return await ActivityCheckInHandlers.ResetAllActivityCheckIns(eventId, activityId, httpContext, db, ct);
    }

    internal static async Task<IResult> SetActivityParticipantWillNotAttend(
        string eventId,
        string activityId,
        string participantId,
        ActivityParticipantWillNotAttendRequest request,
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

        if (!Guid.TryParse(activityId, out var activityGuid))
        {
            return EventsHelpers.BadRequest("Invalid activity id.");
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        if (request is null || !request.WillNotAttend.HasValue)
        {
            return EventsHelpers.BadRequest("willNotAttend is required.");
        }

        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var activityExists = await db.EventActivities.AsNoTracking()
            .AnyAsync(x => x.Id == activityGuid && x.EventId == id && x.OrganizationId == orgId, ct);
        if (!activityExists)
        {
            return Results.NotFound(new { message = "Activity not found." });
        }

        var participant = await db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);
        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var entity = await db.ParticipantActivityWillNotAttend
            .FirstOrDefaultAsync(x => x.ParticipantId == participantGuid && x.ActivityId == activityGuid, ct);

        if (entity is null)
        {
            entity = new ParticipantActivityWillNotAttendEntity
            {
                Id = Guid.NewGuid(),
                ParticipantId = participantGuid,
                ActivityId = activityGuid,
                WillNotAttend = request.WillNotAttend.Value,
                CreatedAt = DateTime.UtcNow
            };
            db.ParticipantActivityWillNotAttend.Add(entity);
        }
        else
        {
            entity.WillNotAttend = request.WillNotAttend.Value;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var lastLog = await db.ActivityParticipantLogs.AsNoTracking()
            .Where(x => x.OrganizationId == orgId
                        && x.EventId == id
                        && x.ActivityId == activityGuid
                        && x.ParticipantId == participantGuid
                        && (x.Result == "Success" || x.Result == "AlreadyCheckedIn"))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ActivityLastLogDto(
                x.Direction,
                x.Method,
                x.Result,
                x.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)))
            .FirstOrDefaultAsync(ct);

        var isCheckedIn = lastLog != null && lastLog.Direction == "Entry" && (lastLog.Result == "Success" || lastLog.Result == "AlreadyCheckedIn");
        var activityState = new ActivityParticipantStateDto(isCheckedIn, entity.WillNotAttend, lastLog);

        return Results.Ok(new ActivityParticipantWillNotAttendResponse(participantGuid, entity.WillNotAttend, activityState));
    }

    internal static async Task<IResult> GetEventItems(
        string eventId,
        bool? includeInactive,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventItemsHandlers.GetItems(eventId, includeInactive, httpContext, db, ct);
    }

    internal static async Task<IResult> CreateEventItem(
        string eventId,
        CreateEventItemRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventItemsHandlers.CreateItem(eventId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> UpdateEventItem(
        string eventId,
        string itemId,
        UpdateEventItemRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventItemsHandlers.UpdateItem(eventId, itemId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> DeleteEventItem(
        string eventId,
        string itemId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventItemsHandlers.DeleteItem(eventId, itemId, httpContext, db, ct);
    }

    internal static async Task<IResult> PostItemAction(
        string eventId,
        string itemId,
        ItemActionRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventItemsHandlers.PostAction(eventId, itemId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> GetItemParticipantsTable(
        string eventId,
        string itemId,
        string? query,
        string? status,
        int? page,
        int? pageSize,
        string? sort,
        string? dir,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError)) return parseError!;
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return Results.NotFound(new { message = "Event not found." });
        return await EventItemsHandlers.GetParticipantsTable(eventId, itemId, query, status, page, pageSize, sort, dir, httpContext, db, ct);
    }

    private static async Task<(IResult? Error, Guid EventId, Guid OrgId)> EnsureGuideEventAccess(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return (error, default, default);
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError)) return (orgError, default, default);
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var parseError)) return (parseError, default, default);
        var hasAccess = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .AnyAsync(x => x.Id == eventGuid && x.EventGuides.Any(g => g.GuideUserId == userId) && x.OrganizationId == orgId && !x.IsDeleted, ct);
        if (!hasAccess) return (Results.NotFound(new { message = "Event not found." }), default, default);
        return (null, eventGuid, orgId);
    }

    internal static async Task<IResult> GetEvent(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, id, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        var entity = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Results.NotFound(new { message = "Event not found." });
        return Results.Ok(EventsHelpers.ToDto(entity));
    }

    internal static async Task<IResult> GetEventDays(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.GetEventDays(eventId, httpContext, db, ct);
    }

    internal static async Task<IResult> CreateEventDay(
        string eventId,
        CreateEventDayRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.CreateEventDay(eventId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> UpdateEventDay(
        string eventId,
        string dayId,
        UpdateEventDayRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.UpdateEventDay(eventId, dayId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> DeleteEventDay(
        string eventId,
        string dayId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.DeleteEventDay(eventId, dayId, httpContext, db, ct);
    }

    internal static async Task<IResult> GetEventActivities(
        string eventId,
        string dayId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.GetEventActivities(eventId, dayId, httpContext, db, ct);
    }

    internal static async Task<IResult> CreateEventActivity(
        string eventId,
        string dayId,
        CreateEventActivityRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.CreateEventActivity(eventId, dayId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> UpdateEventActivity(
        string eventId,
        string activityId,
        UpdateEventActivityRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.UpdateEventActivity(eventId, activityId, request, httpContext, db, ct);
    }

    internal static async Task<IResult> DeleteEventActivity(
        string eventId,
        string activityId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.DeleteEventActivity(eventId, activityId, httpContext, db, ct);
    }
}
