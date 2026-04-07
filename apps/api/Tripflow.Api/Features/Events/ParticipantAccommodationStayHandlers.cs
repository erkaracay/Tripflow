using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class ParticipantAccommodationStayHandlers
{
    internal static async Task<IResult> GetStays(
        string eventId,
        Guid participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eid, out var parseError))
            return parseError!;

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;

        var participantExists = await db.Participants.AsNoTracking()
            .AnyAsync(x => x.Id == participantId && x.EventId == eid && x.OrganizationId == orgId, ct);
        if (!participantExists)
            return Results.NotFound(new { message = "Participant not found." });

        var stays = await db.ParticipantAccommodationStays
            .AsNoTracking()
            .Where(x => x.ParticipantId == participantId)
            .OrderBy(x => x.CheckIn == null)
            .ThenBy(x => x.CheckIn)
            .ToListAsync(ct);

        var tabIds = stays.Select(x => x.EventAccommodationId).Distinct().ToList();
        var tabs = await db.EventDocTabs.AsNoTracking()
            .Where(x => tabIds.Contains(x.Id))
            .ToListAsync(ct);
        var tabById = tabs.ToDictionary(x => x.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Bulk-load roommates for all room groups in one query
        var roomGroups = stays
            .Where(x => !string.IsNullOrEmpty(x.RoomNo))
            .Select(x => new { x.EventAccommodationId, x.RoomNo })
            .Distinct()
            .ToList();

        var roommatesLookup = new Dictionary<(Guid, string?), string[]>();
        if (roomGroups.Count > 0)
        {
            var accommodationIds = roomGroups.Select(g => g.EventAccommodationId).Distinct().ToList();
            var roomNos = roomGroups.Select(g => g.RoomNo).Distinct().ToList();

            var candidates = await db.ParticipantAccommodationStays
                .AsNoTracking()
                .Where(x =>
                    x.EventId == eid &&
                    accommodationIds.Contains(x.EventAccommodationId) &&
                    roomNos.Contains(x.RoomNo) &&
                    x.ParticipantId != participantId &&
                    x.RoomNo != null && x.RoomNo != "")
                .Select(x => new { x.EventAccommodationId, x.RoomNo, x.ParticipantId })
                .ToListAsync(ct);

            var candidateParticipantIds = candidates.Select(x => x.ParticipantId).Distinct().ToList();
            var nameById = await db.Participants.AsNoTracking()
                .Where(x => candidateParticipantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

            foreach (var g in roomGroups)
            {
                var names = candidates
                    .Where(x => x.EventAccommodationId == g.EventAccommodationId && x.RoomNo == g.RoomNo)
                    .Take(10)
                    .Select(x => nameById.TryGetValue(x.ParticipantId, out var n) ? n : null)
                    .Where(n => n != null)
                    .Select(n => n!)
                    .ToArray();
                roommatesLookup[(g.EventAccommodationId, g.RoomNo)] = names;
            }
        }

        var dtos = stays.Select(s => BuildDtoFromLoaded(s, tabById, today, roommatesLookup)).ToArray();

        return Results.Ok(dtos);
    }

    internal static async Task<IResult> CreateStay(
        string eventId,
        Guid participantId,
        UpsertParticipantAccommodationStayRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eid, out var parseError))
            return parseError!;

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;

        var @event = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == eid && x.OrganizationId == orgId, ct);
        if (@event is null)
            return Results.NotFound(new { message = "Event not found." });

        var participantExists = await db.Participants.AsNoTracking()
            .AnyAsync(x => x.Id == participantId && x.EventId == eid && x.OrganizationId == orgId, ct);
        if (!participantExists)
            return Results.NotFound(new { message = "Participant not found." });

        var validationError = await ValidateRequest(request, eid, orgId, participantId, null, @event, db, ct);
        if (validationError is not null)
            return validationError;

        var stay = new ParticipantAccommodationStayEntity
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            EventAccommodationId = request.EventAccommodationId!.Value,
            OrganizationId = orgId,
            EventId = eid,
            RoomNo = NullIfEmpty(request.RoomNo),
            RoomType = NullIfEmpty(request.RoomType),
            BoardType = NullIfEmpty(request.BoardType),
            PersonNo = NullIfEmpty(request.PersonNo),
            CheckIn = ParseOptionalDate(request.CheckIn),
            CheckOut = ParseOptionalDate(request.CheckOut),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.ParticipantAccommodationStays.Add(stay);
        await db.SaveChangesAsync(ct);

        await SyncFirstStayToDetails(db, participantId, ct);

        var dto = await BuildDtoAsync(stay, db, participantId, eid, ct);
        return Results.Created($"/api/events/{eventId}/participants/{participantId}/stays/{stay.Id}", dto);
    }

    internal static async Task<IResult> UpdateStay(
        string eventId,
        Guid participantId,
        Guid stayId,
        UpsertParticipantAccommodationStayRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eid, out var parseError))
            return parseError!;

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;

        var @event = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == eid && x.OrganizationId == orgId, ct);
        if (@event is null)
            return Results.NotFound(new { message = "Event not found." });

        var stay = await db.ParticipantAccommodationStays
            .FirstOrDefaultAsync(x => x.Id == stayId && x.ParticipantId == participantId && x.EventId == eid && x.OrganizationId == orgId, ct);
        if (stay is null)
            return Results.NotFound(new { message = "Stay not found." });

        var validationError = await ValidateRequest(request, eid, orgId, participantId, stayId, @event, db, ct);
        if (validationError is not null)
            return validationError;

        stay.EventAccommodationId = request.EventAccommodationId!.Value;
        stay.RoomNo = NullIfEmpty(request.RoomNo);
        stay.RoomType = NullIfEmpty(request.RoomType);
        stay.BoardType = NullIfEmpty(request.BoardType);
        stay.PersonNo = NullIfEmpty(request.PersonNo);
        stay.CheckIn = ParseOptionalDate(request.CheckIn);
        stay.CheckOut = ParseOptionalDate(request.CheckOut);
        stay.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await SyncFirstStayToDetails(db, participantId, ct);

        var dto = await BuildDtoAsync(stay, db, participantId, eid, ct);
        return Results.Ok(dto);
    }

    internal static async Task<IResult> DeleteStay(
        string eventId,
        Guid participantId,
        Guid stayId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eid, out var parseError))
            return parseError!;

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;

        var stay = await db.ParticipantAccommodationStays
            .FirstOrDefaultAsync(x => x.Id == stayId && x.ParticipantId == participantId && x.EventId == eid && x.OrganizationId == orgId, ct);
        if (stay is null)
            return Results.NotFound(new { message = "Stay not found." });

        db.ParticipantAccommodationStays.Remove(stay);
        await db.SaveChangesAsync(ct);

        await SyncFirstStayToDetails(db, participantId, ct);

        return Results.NoContent();
    }

    // --- Shared ---

    internal static async Task SyncFirstStayToDetails(TripflowDbContext db, Guid participantId, CancellationToken ct)
    {
        var firstStay = await db.ParticipantAccommodationStays
            .Where(x => x.ParticipantId == participantId)
            .OrderBy(x => x.CheckIn == null)
            .ThenBy(x => x.CheckIn)
            .FirstOrDefaultAsync(ct);

        var details = await db.ParticipantDetails
            .FirstOrDefaultAsync(x => x.ParticipantId == participantId, ct);

        if (details is null)
        {
            if (firstStay is null)
                return;

            details = new ParticipantDetailsEntity { ParticipantId = participantId };
            db.ParticipantDetails.Add(details);
        }

        details.AccommodationDocTabId = firstStay?.EventAccommodationId;
        details.RoomNo = firstStay?.RoomNo;
        details.RoomType = firstStay?.RoomType;
        details.BoardType = firstStay?.BoardType;
        details.PersonNo = firstStay?.PersonNo;
        details.HotelCheckInDate = firstStay?.CheckIn;
        details.HotelCheckOutDate = firstStay?.CheckOut;

        await db.SaveChangesAsync(ct);
    }

    // --- Private helpers ---

    private static async Task<IResult?> ValidateRequest(
        UpsertParticipantAccommodationStayRequest request,
        Guid eventId,
        Guid orgId,
        Guid participantId,
        Guid? excludeStayId,
        EventEntity @event,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request.EventAccommodationId is null)
            return EventsHelpers.BadRequest("eventAccommodationId", "eventAccommodationId", "EventAccommodationId is required.");

        var tab = await db.EventDocTabs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.EventAccommodationId.Value && x.EventId == eventId && x.OrganizationId == orgId, ct);
        if (tab is null || !string.Equals(tab.Type, "hotel", StringComparison.OrdinalIgnoreCase))
            return EventsHelpers.BadRequest("invalid_accommodation", "eventAccommodationId", "EventAccommodationId must reference a hotel-type tab for this event.");

        DateOnly? checkIn = null;
        DateOnly? checkOut = null;

        if (!string.IsNullOrWhiteSpace(request.CheckIn))
        {
            if (!EventsHelpers.TryParseDate(request.CheckIn, out var ci))
                return EventsHelpers.BadRequest("invalid_date", "checkIn", "checkIn must be in YYYY-MM-DD format.");
            checkIn = ci;
        }

        if (!string.IsNullOrWhiteSpace(request.CheckOut))
        {
            if (!EventsHelpers.TryParseDate(request.CheckOut, out var co))
                return EventsHelpers.BadRequest("invalid_date", "checkOut", "checkOut must be in YYYY-MM-DD format.");
            checkOut = co;
        }

        if (checkIn.HasValue && checkOut.HasValue && checkOut.Value < checkIn.Value)
            return EventsHelpers.BadRequest("checkout_before_checkin", "checkOut", "checkOut must be on or after checkIn.");

        if (checkIn.HasValue && (checkIn.Value < @event.StartDate || checkIn.Value > @event.EndDate))
            return EventsHelpers.BadRequest("date_out_of_range", "checkIn", "checkIn must be within the event date range.");

        if (checkOut.HasValue && (checkOut.Value < @event.StartDate || checkOut.Value > @event.EndDate))
            return EventsHelpers.BadRequest("date_out_of_range", "checkOut", "checkOut must be within the event date range.");

        if (checkIn.HasValue && checkOut.HasValue)
        {
            var overlapQuery = db.ParticipantAccommodationStays
                .AsNoTracking()
                .Where(x =>
                    x.ParticipantId == participantId &&
                    x.CheckIn.HasValue && x.CheckOut.HasValue &&
                    x.CheckIn.Value <= checkOut.Value &&
                    x.CheckOut.Value >= checkIn.Value);

            if (excludeStayId.HasValue)
                overlapQuery = overlapQuery.Where(x => x.Id != excludeStayId.Value);

            if (await overlapQuery.AnyAsync(ct))
                return EventsHelpers.BadRequest("overlap", "checkIn", "Date range overlaps with an existing stay.");
        }

        return null;
    }

    private static async Task<ParticipantAccommodationStayDto> BuildDtoAsync(
        ParticipantAccommodationStayEntity stay,
        TripflowDbContext db,
        Guid participantId,
        Guid eventId,
        CancellationToken ct)
    {
        var tab = await db.EventDocTabs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == stay.EventAccommodationId, ct);

        var title = tab?.Title ?? string.Empty;
        JsonElement? content = TryParseJson(tab?.ContentJson);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isCurrent = stay.CheckIn.HasValue && stay.CheckOut.HasValue &&
                        today >= stay.CheckIn.Value && today <= stay.CheckOut.Value;

        int? nightCount = null;
        if (stay.CheckIn.HasValue && stay.CheckOut.HasValue)
            nightCount = stay.CheckOut.Value.DayNumber - stay.CheckIn.Value.DayNumber;

        var roommates = Array.Empty<string>();
        if (!string.IsNullOrEmpty(stay.RoomNo))
        {
            var rmIds = await db.ParticipantAccommodationStays.AsNoTracking()
                .Where(x =>
                    x.EventId == eventId &&
                    x.EventAccommodationId == stay.EventAccommodationId &&
                    x.RoomNo == stay.RoomNo &&
                    x.ParticipantId != participantId)
                .Select(x => x.ParticipantId)
                .Take(10)
                .ToListAsync(ct);

            if (rmIds.Count > 0)
            {
                roommates = await db.Participants.AsNoTracking()
                    .Where(x => rmIds.Contains(x.Id))
                    .OrderBy(x => x.FullName)
                    .Select(x => x.FullName)
                    .ToArrayAsync(ct);
            }
        }

        return new ParticipantAccommodationStayDto(
            stay.Id,
            stay.EventAccommodationId,
            title,
            content,
            stay.RoomNo,
            stay.RoomType,
            stay.BoardType,
            stay.PersonNo,
            stay.CheckIn?.ToString("yyyy-MM-dd"),
            stay.CheckOut?.ToString("yyyy-MM-dd"),
            nightCount,
            isCurrent,
            roommates);
    }

    private static ParticipantAccommodationStayDto BuildDtoFromLoaded(
        ParticipantAccommodationStayEntity stay,
        Dictionary<Guid, EventDocTabEntity> tabById,
        DateOnly today,
        Dictionary<(Guid, string?), string[]> roommatesLookup)
    {
        tabById.TryGetValue(stay.EventAccommodationId, out var tab);
        var title = tab?.Title ?? string.Empty;
        JsonElement? content = TryParseJson(tab?.ContentJson);

        var isCurrent = stay.CheckIn.HasValue && stay.CheckOut.HasValue &&
                        today >= stay.CheckIn.Value && today <= stay.CheckOut.Value;

        int? nightCount = null;
        if (stay.CheckIn.HasValue && stay.CheckOut.HasValue)
            nightCount = stay.CheckOut.Value.DayNumber - stay.CheckIn.Value.DayNumber;

        var roommates = !string.IsNullOrEmpty(stay.RoomNo) &&
                        roommatesLookup.TryGetValue((stay.EventAccommodationId, stay.RoomNo), out var rm)
            ? rm
            : Array.Empty<string>();

        return new ParticipantAccommodationStayDto(
            stay.Id,
            stay.EventAccommodationId,
            title,
            content,
            stay.RoomNo,
            stay.RoomType,
            stay.BoardType,
            stay.PersonNo,
            stay.CheckIn?.ToString("yyyy-MM-dd"),
            stay.CheckOut?.ToString("yyyy-MM-dd"),
            nightCount,
            isCurrent,
            roommates);
    }

    private static JsonElement? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return null; }
    }

    private static DateOnly? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return EventsHelpers.TryParseDate(value, out var d) ? d : null;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
