using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

internal static class PortalLoginHandlers
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private static readonly ConcurrentDictionary<string, List<DateTime>> Attempts = new();

    internal static async Task<IResult> Login(
        PortalLoginRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var code = NormalizeCode(request.EventAccessCode);
        var tcNo = NormalizeTcNo(request.TcNo);

        var attemptKey = $"{code}|{GetClientIp(httpContext)}";
        if (IsRateLimited(attemptKey, out var retryAfter))
        {
            httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
            return Results.Json(new { code = "rate_limited", retryAfterSeconds = retryAfter }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "invalid_access_code_format" });
        }

        if (string.IsNullOrWhiteSpace(tcNo) || tcNo.Length != 11)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "invalid_tcno_format" });
        }

        var matchingEvents = await db.Events.AsNoTracking()
            .Where(x => x.EventAccessCode == code)
            .ToListAsync(ct);

        if (matchingEvents.Count == 0)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "invalid_event_access_code" });
        }

        if (matchingEvents.Count > 1)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "ambiguous_event_access_code" });
        }

        var eventEntity = matchingEvents[0];
        var participants = await db.Participants
            .Where(x => x.EventId == eventEntity.Id && x.OrganizationId == eventEntity.OrganizationId && x.TcNo == tcNo)
            .ToListAsync(ct);

        if (participants.Count == 0)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "tcno_not_found" });
        }

        if (participants.Count > 1)
        {
            RegisterFailure(attemptKey);
            return Results.Conflict(new { code = "ambiguous_tcno" });
        }

        var participant = participants[0];

        var now = DateTime.UtcNow;
        var token = PortalSessionHelpers.CreateToken();
        var tokenHash = PortalSessionHelpers.HashToken(token);
        var expiresAt = now.AddDays(7);

        var session = await db.PortalSessions.FirstOrDefaultAsync(
            x => x.EventId == eventEntity.Id && x.ParticipantId == participant.Id, ct);

        if (session is null)
        {
            session = new PortalSessionEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = participant.OrganizationId,
                EventId = eventEntity.Id,
                ParticipantId = participant.Id,
                TokenHash = tokenHash,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                LastSeenAt = now
            };
            db.PortalSessions.Add(session);
        }
        else
        {
            session.TokenHash = tokenHash;
            session.CreatedAt = now;
            session.ExpiresAt = expiresAt;
            session.LastSeenAt = now;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new PortalLoginResponse(token, expiresAt, eventEntity.Id, participant.Id));
    }

    internal static async Task<IResult> GetMe(
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var session = await PortalSessionHelpers.GetValidSessionAsync(httpContext, db, ct);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var participant = session.Participant;
        if (participant is null)
        {
            return Results.Unauthorized();
        }

        var eventEntity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == session.EventId, ct);
        if (eventEntity is null)
        {
            return Results.Unauthorized();
        }

        session.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var portal = await GetPortalInfoAsync(db, eventEntity.Id, eventEntity.OrganizationId, ct);
        if (portal is null)
        {
            return Results.NotFound();
        }

        var schedule = await EventsHandlers.BuildScheduleAsync(eventEntity.Id, eventEntity.OrganizationId, db, ct);
        var docs = await BuildDocsAsync(db, eventEntity, participant, ct);

        var response = new PortalMeResponse(
            new PortalEventSummary(
                eventEntity.Id,
                eventEntity.Name,
                eventEntity.StartDate.ToString("yyyy-MM-dd"),
                eventEntity.EndDate.ToString("yyyy-MM-dd"),
                eventEntity.LogoUrl),
            new PortalParticipantSummaryFull(
                participant.Id,
                participant.FullName,
                participant.Phone,
                participant.Email,
                participant.TcNo,
                participant.BirthDate.ToString("yyyy-MM-dd"),
                participant.Gender.ToString(),
                participant.CheckInCode),
            portal,
            schedule,
            docs);

        return Results.Ok(response);
    }

    internal static async Task<IResult> ResolveEventAccessCode(
        string? eventAccessCode,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var code = NormalizeCode(eventAccessCode);
        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            return Results.BadRequest(new { code = "invalid_event_access_code_format" });
        }

        var eventEntity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventAccessCode == code, ct);

        if (eventEntity is null)
        {
            return Results.NotFound(new { code = "event_access_code_not_found" });
        }

        return Results.Ok(new PortalResolveEventResponse(eventEntity.Id, eventEntity.Name));
    }

    private static string NormalizeCode(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

    private static string NormalizeTcNo(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string GetClientIp(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async Task<PortalDocsResponse> BuildDocsAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        ParticipantEntity participant,
        CancellationToken ct)
    {
        var details = await db.ParticipantDetails.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ParticipantId == participant.Id, ct);

        var allTabs = await db.EventDocTabs.AsNoTracking()
            .Where(x => x.EventId == eventEntity.Id && x.OrganizationId == eventEntity.OrganizationId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var insuranceDefaults = allTabs.FirstOrDefault(x => x.Type == "Insurance");
        var insuranceInfo = BuildInsuranceInfo(insuranceDefaults?.ContentJson, details);

        var transferOutbound = BuildTransferInfo(
            details?.ArrivalTransferPickupTime,
            details?.ArrivalTransferPickupPlace,
            details?.ArrivalTransferDropoffPlace,
            details?.ArrivalTransferVehicle,
            details?.ArrivalTransferPlate,
            details?.ArrivalTransferDriverInfo,
            details?.ArrivalTransferNote);

        var transferReturn = BuildTransferInfo(
            details?.ReturnTransferPickupTime,
            details?.ReturnTransferPickupPlace,
            details?.ReturnTransferDropoffPlace,
            details?.ReturnTransferVehicle,
            details?.ReturnTransferPlate,
            details?.ReturnTransferDriverInfo,
            details?.ReturnTransferNote);

        var travel = new PortalParticipantTravel(
            details?.RoomNo,
            details?.RoomType,
            details?.BoardType,
            details?.HotelCheckInDate?.ToString("yyyy-MM-dd"),
            details?.HotelCheckOutDate?.ToString("yyyy-MM-dd"),
            details?.TicketNo,
            details is null
                ? null
                : new PortalFlightInfo(
                    details.ArrivalAirline,
                    details.ArrivalDepartureAirport,
                    details.ArrivalArrivalAirport,
                    details.ArrivalFlightCode,
                    details.ArrivalDepartureTime?.ToString("HH:mm"),
                    details.ArrivalArrivalTime?.ToString("HH:mm"),
                    details.ArrivalPnr,
                    details.ArrivalBaggagePieces,
                    details.ArrivalBaggageTotalKg),
            details is null
                ? null
                : new PortalFlightInfo(
                    details.ReturnAirline,
                    details.ReturnDepartureAirport,
                    details.ReturnArrivalAirport,
                    details.ReturnFlightCode,
                    details.ReturnDepartureTime?.ToString("HH:mm"),
                    details.ReturnArrivalTime?.ToString("HH:mm"),
                    details.ReturnPnr,
                    details.ReturnBaggagePieces,
                    details.ReturnBaggageTotalKg),
            transferOutbound,
            transferReturn,
            insuranceInfo);

        var tabDtos = allTabs
            .Where(tab => tab.IsActive)
            .Select(tab => new PortalDocTabDto(
            tab.Id,
            tab.Title,
            tab.Type,
            tab.SortOrder,
            ParseContentJson(tab.ContentJson)))
            .ToArray();

        return new PortalDocsResponse(tabDtos, travel);
    }

    private static PortalInsuranceInfo? BuildInsuranceInfo(string? contentJson, ParticipantDetailsEntity? details)
    {
        var defaults = ParseContentJson(contentJson);

        var defaultCompany = ReadStringProperty(defaults, "companyName");
        var defaultPolicy = ReadStringProperty(defaults, "policyNo");
        var defaultStart = ReadStringProperty(defaults, "startDate");
        var defaultEnd = ReadStringProperty(defaults, "endDate");

        var company = string.IsNullOrWhiteSpace(details?.InsuranceCompanyName) ? defaultCompany : details!.InsuranceCompanyName;
        var policyNo = string.IsNullOrWhiteSpace(details?.InsurancePolicyNo) ? defaultPolicy : details!.InsurancePolicyNo;
        var start = details?.InsuranceStartDate?.ToString("yyyy-MM-dd") ?? defaultStart;
        var end = details?.InsuranceEndDate?.ToString("yyyy-MM-dd") ?? defaultEnd;

        if (string.IsNullOrWhiteSpace(company)
            && string.IsNullOrWhiteSpace(policyNo)
            && string.IsNullOrWhiteSpace(start)
            && string.IsNullOrWhiteSpace(end))
        {
            return null;
        }

        return new PortalInsuranceInfo(company, policyNo, start, end);
    }

    private static PortalTransferInfo? BuildTransferInfo(
        TimeOnly? pickupTime,
        string? pickupPlace,
        string? dropoffPlace,
        string? vehicle,
        string? plate,
        string? driverInfo,
        string? note)
    {
        if (pickupTime is null
            && string.IsNullOrWhiteSpace(pickupPlace)
            && string.IsNullOrWhiteSpace(dropoffPlace)
            && string.IsNullOrWhiteSpace(vehicle)
            && string.IsNullOrWhiteSpace(plate)
            && string.IsNullOrWhiteSpace(driverInfo)
            && string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        return new PortalTransferInfo(
            pickupTime?.ToString("HH:mm"),
            pickupPlace,
            dropoffPlace,
            vehicle,
            plate,
            driverInfo,
            note);
    }

    private static JsonElement ParseContentJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }

    private static string? ReadStringProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(name, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return prop.ToString();
    }

    private static bool IsRateLimited(string key, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        var now = DateTime.UtcNow;
        var list = Attempts.GetOrAdd(key, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(x => now - x > Window);
            if (list.Count < MaxAttempts)
            {
                return false;
            }

            var oldest = list.Min();
            retryAfterSeconds = (int)Math.Ceiling((Window - (now - oldest)).TotalSeconds);
            return true;
        }
    }

    private static void RegisterFailure(string key)
    {
        var now = DateTime.UtcNow;
        var list = Attempts.GetOrAdd(key, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(x => now - x > Window);
            list.Add(now);
        }
    }

    private static async Task<EventPortalInfo?> GetPortalInfoAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        CancellationToken ct)
    {
        var portalEntity = await db.EventPortals.FirstOrDefaultAsync(x => x.EventId == eventId, ct);
        if (portalEntity is null)
        {
            var eventEntity = await db.Events.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == eventId && x.OrganizationId == organizationId, ct);
            if (eventEntity is null)
            {
                return null;
            }

            var fallback = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            var json = System.Text.Json.JsonSerializer.Serialize(fallback, EventsHelpers.JsonOptions);
            portalEntity = new EventPortalEntity
            {
                EventId = eventId,
                OrganizationId = organizationId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            };
            db.EventPortals.Add(portalEntity);
            await db.SaveChangesAsync(ct);
            return fallback;
        }

        var portal = EventsHelpers.TryDeserializePortal(portalEntity.PortalJson);
        if (portal is null)
        {
            var eventEntity = await db.Events.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == eventId && x.OrganizationId == organizationId, ct);
            if (eventEntity is null)
            {
                return null;
            }

            portal = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            portalEntity.PortalJson = System.Text.Json.JsonSerializer.Serialize(portal, EventsHelpers.JsonOptions);
            portalEntity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return portal;
    }
}
