using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Helpers;

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

        var events = await db.Events.AsNoTracking()
            .Include(x => x.EventGuides)
            .Where(x => x.EventGuides.Any(g => g.GuideUserId == userId) && !x.IsDeleted)
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
                x.EventAccessCode,
                x.Organization.Name))
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
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
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
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        return await EventsHandlers.SetParticipantWillNotAttend(eventId, participantId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> ResolveParticipantByCode(
        string eventId,
        string? code,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
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
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
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
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        var schedule = await EventsHandlers.BuildScheduleAsync(id, orgId, db, ct);
        return Results.Ok(schedule);
    }

    internal static async Task<IResult> GetAccommodationSegments(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        var items = await db.EventAccommodationSegments.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new AccommodationSegmentDto(
                x.Id,
                x.DefaultAccommodationDocTabId,
                x.DefaultAccommodationDocTab.Title,
                x.StartDate.ToString("yyyy-MM-dd"),
                x.EndDate.ToString("yyyy-MM-dd"),
                x.SortOrder))
            .ToArrayAsync(ct);

        return Results.Ok(items);
    }

    internal static async Task<IResult> GetAccommodationSegmentParticipants(
        string eventId,
        Guid segmentId,
        string? query,
        string? accommodationFilter,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        var segmentContext = await AccommodationSegmentsReadHelpers.GetSegmentContextAsync(db, id, orgId, segmentId, ct);
        if (segmentContext is null)
        {
            return Results.NotFound(new { message = "Accommodation segment not found." });
        }

        var resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            resolvedPage = 1;
        }

        var resolvedPageSize = pageSize.GetValueOrDefault(50);
        if (resolvedPageSize < 1)
        {
            resolvedPageSize = 50;
        }
        resolvedPageSize = Math.Min(resolvedPageSize, 200);

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId);

        var baseQuery = AccommodationSegmentsReadHelpers.BuildSegmentParticipantsQuery(db, participantsQuery, segmentContext);

        var availableAccommodations = await baseQuery
            .Select(x => new { x.EffectiveAccommodationDocTabId, x.EffectiveAccommodationTitle })
            .Distinct()
            .OrderBy(x => x.EffectiveAccommodationTitle)
            .Select(x => new GuideAccommodationOptionDto(
                x.EffectiveAccommodationDocTabId,
                x.EffectiveAccommodationTitle))
            .ToArrayAsync(ct);

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            baseQuery = baseQuery.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || EF.Functions.ILike(x.TcNo, pattern)
                || (x.RoomNo != null && EF.Functions.ILike(x.RoomNo, pattern)));
        }

        var accommodationFilterValue = accommodationFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(accommodationFilterValue)
            && Guid.TryParse(accommodationFilterValue, out var accommodationDocTabId))
        {
            baseQuery = baseQuery.Where(x => x.EffectiveAccommodationDocTabId == accommodationDocTabId);
        }

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.TcNo)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToArrayAsync(ct);

        var roommateLookup = await AccommodationSegmentsReadHelpers.BuildSegmentRoommatesLookupAsync(
            db,
            segmentContext,
            items,
            ct);

        return Results.Ok(new GuideAccommodationParticipantResponseDto(
            resolvedPage,
            resolvedPageSize,
            total,
            items
                .Select(x => new GuideAccommodationParticipantDto(
                    x.ParticipantId,
                    x.FullName,
                    x.TcNo,
                    x.EffectiveAccommodationDocTabId,
                    x.EffectiveAccommodationTitle,
                    x.UsesOverride,
                    x.RoomNo,
                    x.RoomType,
                    x.BoardType,
                    x.PersonNo,
                    roommateLookup.GetValueOrDefault(x.ParticipantId, [])))
                .ToArray(),
            availableAccommodations));
    }

    internal static async Task<IResult> CheckInByCode(
        string eventId,
        CheckInCodeRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error))
        {
            return error!;
        }

        var (eventError, _, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        var actorRole = user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = null;
        }

        return await EventsHandlers.CheckInByCodeForOrg(orgId, eventId, request, userId, actorRole, ipAddress, userAgent, httpContext, auditService, db, ct);
    }

    internal static async Task<IResult> UndoCheckIn(
        string eventId,
        CheckInUndoRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        return await EventsHandlers.UndoCheckInForOrg(orgId, eventId, request, httpContext, auditService, db, ct);
    }

    internal static async Task<IResult> ResetAllCheckIns(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        return await EventsHandlers.ResetAllCheckInsForOrg(orgId, eventId, httpContext, auditService, db, ct);
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
            details.ReturnTransferNote,
            details.AccommodationDocTabId);
    }

    internal static async Task<IResult> GetActivitiesForCheckIn(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await EventsHandlers.GetActivitiesForCheckIn(eventId, httpContext, db, ct);
    }

    internal static async Task<IResult> PostActivityCheckIn(
        string eventId,
        string activityId,
        ActivityCheckInRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await ActivityCheckInHandlers.PostCheckIn(eventId, activityId, request, httpContext, db, auditService, ct);
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
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await ActivityCheckInHandlers.GetParticipantsTable(eventId, activityId, query, status, page, pageSize, sort, dir, httpContext, db, ct);
    }

    internal static async Task<IResult> GetMealSummary(
        string eventId,
        string activityId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.GetMealSummary(eventId, activityId, httpContext, db, ct);
    }

    internal static async Task<IResult> GetMealGroups(
        string eventId,
        string activityId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.GetMealGroups(eventId, activityId, httpContext, db, ct);
    }

    internal static async Task<IResult> CreateMealGroup(
        string eventId,
        string activityId,
        CreateMealGroupRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.CreateMealGroupForRoute(eventId, activityId, request, "/api/guide/events", httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> UpdateMealGroup(
        string eventId,
        string groupId,
        UpdateMealGroupRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.UpdateMealGroup(eventId, groupId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> DeleteMealGroup(
        string eventId,
        string groupId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.DeleteMealGroup(eventId, groupId, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> CreateMealOption(
        string eventId,
        string groupId,
        CreateMealOptionRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.CreateMealOptionForRoute(eventId, groupId, request, "/api/guide/events", httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> UpdateMealOption(
        string eventId,
        string optionId,
        UpdateMealOptionRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.UpdateMealOption(eventId, optionId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> DeleteMealOption(
        string eventId,
        string optionId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.DeleteMealOption(eventId, optionId, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> GetMealChoices(
        string eventId,
        string activityId,
        string? groupId,
        string? optionId,
        string? q,
        bool? onlyNotes,
        bool? onlyOther,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await MealMenuHandlers.GetMealChoices(eventId, activityId, groupId, optionId, q, onlyNotes, onlyOther, page, pageSize, httpContext, db, ct);
    }

    internal static async Task<IResult> ResetAllActivityCheckIns(
        string eventId,
        string activityId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
            return eventError;

        return await ActivityCheckInHandlers.ResetAllActivityCheckIns(eventId, activityId, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> SetActivityParticipantWillNotAttend(
        string eventId,
        string activityId,
        string participantId,
        ActivityParticipantWillNotAttendRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, id, orgId) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null)
        {
            return eventError;
        }

        return await ActivityCheckInHandlers.SetActivityParticipantWillNotAttend(eventId, activityId, participantId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> GetEventItems(
        string eventId,
        bool? includeInactive,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await EventItemsHandlers.GetItems(eventId, includeInactive, httpContext, db, ct);
    }

    internal static async Task<IResult> CreateEventItem(
        string eventId,
        CreateEventItemRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await EventItemsHandlers.CreateItem(eventId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> UpdateEventItem(
        string eventId,
        string itemId,
        UpdateEventItemRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await EventItemsHandlers.UpdateItem(eventId, itemId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> DeleteEventItem(
        string eventId,
        string itemId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await EventItemsHandlers.DeleteItem(eventId, itemId, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> PostItemAction(
        string eventId,
        string itemId,
        ItemActionRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
        return await EventItemsHandlers.PostAction(eventId, itemId, request, httpContext, db, auditService, ct);
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
        var (eventError, _, _) = await EnsureGuideEventAccess(eventId, httpContext, user, db, ct);
        if (eventError is not null) return eventError;
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
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var parseError)) return (parseError, default, default);
        var eventContext = await db.Events.AsNoTracking()
            .Where(x => x.Id == eventGuid && !x.IsDeleted && x.EventGuides.Any(g => g.GuideUserId == userId))
            .Select(x => new { x.Id, x.OrganizationId })
            .FirstOrDefaultAsync(ct);
        if (eventContext is null) return (Results.NotFound(new { message = "Event not found." }), default, default);

        OrganizationHelpers.ApplyOrganizationContext(httpContext, eventContext.OrganizationId);
        return (null, eventContext.Id, eventContext.OrganizationId);
    }

    private static async Task<(IResult? Error, Guid EventId, Guid OrgId)> EnsureGuideProgramEditAccess(
        string eventId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId, out var error)) return (error, default, default);
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var parseError)) return (parseError, default, default);

        var eventContext = await db.Events.AsNoTracking()
            .Where(x => x.Id == eventGuid && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                Assigned = x.EventGuides.Any(g => g.GuideUserId == userId)
            })
            .FirstOrDefaultAsync(ct);

        if (eventContext is null)
        {
            return (Results.NotFound(new { message = "Event not found." }), default, default);
        }

        if (!eventContext.Assigned)
        {
            return (Results.StatusCode(StatusCodes.Status403Forbidden), default, default);
        }

        OrganizationHelpers.ApplyOrganizationContext(httpContext, eventContext.OrganizationId);
        return (null, eventContext.Id, eventContext.OrganizationId);
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
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.GetEventDays(eventId, httpContext, db, ct);
    }

    internal static async Task<IResult> CreateEventDay(
        string eventId,
        CreateEventDayRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.CreateEventDay(eventId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> UpdateEventDay(
        string eventId,
        string dayId,
        UpdateEventDayRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.UpdateEventDay(eventId, dayId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> DeleteEventDay(
        string eventId,
        string dayId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.DeleteEventDay(eventId, dayId, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> GetEventActivities(
        string eventId,
        string dayId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
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
        AuditService auditService,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.CreateEventActivity(eventId, dayId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> UpdateEventActivity(
        string eventId,
        string activityId,
        UpdateEventActivityRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.UpdateEventActivity(eventId, activityId, request, httpContext, db, auditService, ct);
    }

    internal static async Task<IResult> DeleteEventActivity(
        string eventId,
        string activityId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        AuditService auditService,
        CancellationToken ct)
    {
        var (err, _, _) = await EnsureGuideProgramEditAccess(eventId, httpContext, user, db, ct);
        if (err is not null) return err;
        return await EventsHandlers.DeleteEventActivity(eventId, activityId, httpContext, db, auditService, ct);
    }
}
