using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class EventsHandlers
{
    internal static async Task<IResult> GetEvents(
        bool? includeArchived,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var showArchived = includeArchived ?? false;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var error))
        {
            return error!;
        }

        var events = await db.Events.AsNoTracking()
            .Where(x => x.OrganizationId == orgId && (showArchived || !x.IsDeleted))
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new EventListItemDto(
                x.Id,
                x.Name,
                x.StartDate.ToString("yyyy-MM-dd"),
                x.EndDate.ToString("yyyy-MM-dd"),
                db.CheckIns.Count(c => c.EventId == x.Id),
                db.Participants.Count(p => p.EventId == x.Id),
                x.GuideUserId,
                x.IsDeleted))
            .ToArrayAsync(ct);

        return Results.Ok(events);
    }

    internal static async Task<IResult> CreateEvent(
        CreateEventRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return EventsHelpers.BadRequest("Name is required.");
        }

        if (!EventsHelpers.TryParseDate(request.StartDate, out var startDate))
        {
            return EventsHelpers.BadRequest("Start date must be in YYYY-MM-DD format.");
        }

        if (!EventsHelpers.TryParseDate(request.EndDate, out var endDate))
        {
            return EventsHelpers.BadRequest("End date must be in YYYY-MM-DD format.");
        }

        if (endDate < startDate)
        {
            return EventsHelpers.BadRequest("End date must be on or after start date.");
        }

        var entity = new EventEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var portalJson = System.Text.Json.JsonSerializer.Serialize(
            EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(entity)),
            EventsHelpers.JsonOptions);

        db.Events.Add(entity);
        db.EventPortals.Add(new EventPortalEntity
        {
            EventId = entity.Id,
            OrganizationId = orgId,
            PortalJson = portalJson,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var dto = EventsHelpers.ToDto(entity);
        return Results.Created($"/api/events/{dto.Id}", dto);
    }

    internal static async Task<IResult> UpdateEvent(
        string eventId,
        UpdateEventRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return EventsHelpers.BadRequest("Name is required.");
        }

        if (!EventsHelpers.TryParseDate(request.StartDate, out var startDate))
        {
            return EventsHelpers.BadRequest("Start date must be in YYYY-MM-DD format.");
        }

        if (!EventsHelpers.TryParseDate(request.EndDate, out var endDate))
        {
            return EventsHelpers.BadRequest("End date must be in YYYY-MM-DD format.");
        }

        if (endDate < startDate)
        {
            return EventsHelpers.BadRequest("End date must be on or after start date.");
        }

        var entity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        entity.Name = name;
        entity.StartDate = startDate;
        entity.EndDate = endDate;

        await db.SaveChangesAsync(ct);
        return Results.Ok(EventsHelpers.ToDto(entity));
    }

    internal static async Task<IResult> GetEvent(string eventId, TripflowDbContext db, CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var entity = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        return Results.Ok(EventsHelpers.ToDto(entity));
    }

    internal static async Task<IResult> ArchiveEvent(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        if (!entity.IsDeleted)
        {
            entity.IsDeleted = true;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(EventsHelpers.ToDto(entity));
    }

    internal static async Task<IResult> RestoreEvent(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        if (entity.IsDeleted)
        {
            entity.IsDeleted = false;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(EventsHelpers.ToDto(entity));
    }

    internal static async Task<IResult> PurgeEvent(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        if (!entity.IsDeleted)
        {
            return Results.Conflict(new { message = "Event must be archived before purge." });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var participantIds = await db.Participants.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (participantIds.Count > 0)
        {
            var access = db.ParticipantAccesses.Where(x => participantIds.Contains(x.ParticipantId));
            db.ParticipantAccesses.RemoveRange(access);

            var sessions = db.PortalSessions.Where(x => participantIds.Contains(x.ParticipantId));
            db.PortalSessions.RemoveRange(sessions);
        }

        db.CheckIns.RemoveRange(db.CheckIns.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.EventPortals.RemoveRange(db.EventPortals.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.Participants.RemoveRange(db.Participants.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.Events.RemoveRange(db.Events.Where(x => x.Id == id && x.OrganizationId == orgId));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.NoContent();
    }

    internal static async Task<IResult> GetPortal(string eventId, TripflowDbContext db, CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var eventEntity = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (eventEntity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var portalEntity = await db.EventPortals.FirstOrDefaultAsync(x => x.EventId == id, ct);
        if (portalEntity is null)
        {
            var fallback = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            var json = System.Text.Json.JsonSerializer.Serialize(fallback, EventsHelpers.JsonOptions);

            db.EventPortals.Add(new EventPortalEntity
            {
                EventId = id,
                OrganizationId = eventEntity.OrganizationId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            return Results.Ok(fallback);
        }

        var portal = EventsHelpers.TryDeserializePortal(portalEntity.PortalJson);
        if (portal is null)
        {
            portal = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            portalEntity.PortalJson = System.Text.Json.JsonSerializer.Serialize(portal, EventsHelpers.JsonOptions);
            portalEntity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(portal);
    }

    internal static async Task<IResult> VerifyCheckInCode(
        string eventId,
        VerifyCheckInCodeRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var raw = request?.CheckInCode ?? string.Empty;
        var normalized = raw.Trim().ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);

        if (normalized.Length != 8)
        {
            return Results.Ok(new VerifyCheckInCodeResponse(false, null));
        }

        var eventExists = await db.Events.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!eventExists)
        {
            return Results.Ok(new VerifyCheckInCodeResponse(false, null));
        }

        var isValid = await db.Participants.AsNoTracking()
            .AnyAsync(p => p.EventId == id && p.CheckInCode == normalized, ct);

        return Results.Ok(new VerifyCheckInCodeResponse(isValid, isValid ? normalized : null));
    }

    internal static async Task<IResult> SavePortal(
        string eventId,
        EventPortalInfo request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventEntity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (eventEntity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        if (request.Meeting is null)
        {
            return EventsHelpers.BadRequest("Meeting details are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Meeting.Time) ||
            string.IsNullOrWhiteSpace(request.Meeting.Place) ||
            string.IsNullOrWhiteSpace(request.Meeting.MapsUrl))
        {
            return EventsHelpers.BadRequest("Meeting time/place/mapsUrl are required.");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(request, EventsHelpers.JsonOptions);

        var portalEntity = await db.EventPortals
            .FirstOrDefaultAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);
        if (portalEntity is null)
        {
            portalEntity = new EventPortalEntity
            {
                EventId = id,
                OrganizationId = orgId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            };
            db.EventPortals.Add(portalEntity);
        }
        else
        {
            portalEntity.PortalJson = json;
            portalEntity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(request);
    }

    internal static async Task<IResult> AssignGuide(
        string eventId,
        AssignGuideRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (request is null || !request.GuideUserId.HasValue || request.GuideUserId == Guid.Empty)
        {
            return EventsHelpers.BadRequest("Guide user id is required.");
        }

        var eventEntity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (eventEntity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var guideId = request.GuideUserId.Value;
        var guideExists = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == guideId && x.Role == "Guide" && x.OrganizationId == orgId, ct);

        if (!guideExists)
        {
            return EventsHelpers.BadRequest("Guide user not found.");
        }

        eventEntity.GuideUserId = guideId;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { eventId = id, guideUserId = guideId });
    }

    internal static async Task<IResult> GetParticipants(
        string eventId,
        string? query,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
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

    internal static async Task<IResult> CreateParticipant(
        string eventId,
        CreateParticipantRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return EventsHelpers.BadRequest("Full name is required.");
        }

        var phone = request.Phone?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            return EventsHelpers.BadRequest("Phone is required.");
        }

        var code = await EventsHelpers.GenerateUniqueCheckInCodeAsync(db, ct);
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var entity = new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            EventId = id,
            OrganizationId = orgId,
            FullName = fullName,
            Email = request.Email?.Trim(),
            Phone = phone,
            CheckInCode = code,
            CreatedAt = DateTime.UtcNow
        };

        db.Participants.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Participant could not be created. Try again." });
        }

        return Results.Created($"/api/events/{id}/participants/{entity.Id}",
            new ParticipantDto(entity.Id, entity.FullName, entity.Email, entity.Phone, entity.CheckInCode, false));
    }

    internal static async Task<IResult> UpdateParticipant(
        string eventId,
        string participantId,
        UpdateParticipantRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var entity = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return EventsHelpers.BadRequest("Full name is required.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            return EventsHelpers.BadRequest("Phone is required.");
        }

        entity.FullName = fullName;
        entity.Email = email;
        entity.Phone = phone;

        await db.SaveChangesAsync(ct);

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.ParticipantId == entity.Id && x.OrganizationId == orgId, ct);

        return Results.Ok(new ParticipantDto(entity.Id, entity.FullName, entity.Email, entity.Phone, entity.CheckInCode, arrived));
    }

    internal static async Task<IResult> DeleteParticipant(
        string eventId,
        string participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var entity = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var checkIn = await db.CheckIns.FirstOrDefaultAsync(
            x => x.EventId == id && x.ParticipantId == participantGuid && x.OrganizationId == orgId, ct);
        if (checkIn is not null)
        {
            db.CheckIns.Remove(checkIn);
        }

        db.Participants.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    internal static async Task<IResult> CheckIn(
        string eventId,
        CheckInRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await CheckInForOrg(orgId, eventId, request, db, ct);
    }

    internal static async Task<IResult> CheckInForOrg(
        Guid orgId,
        string eventId,
        CheckInRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        ParticipantEntity? participant = null;

        if (request.ParticipantId.HasValue)
        {
            participant = await db.Participants
                .FirstOrDefaultAsync(x => x.Id == request.ParticipantId.Value && x.EventId == id && x.OrganizationId == orgId, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var code = request.Code.Trim().ToUpperInvariant();
            participant = await db.Participants
                .FirstOrDefaultAsync(x => x.EventId == id && x.CheckInCode == code && x.OrganizationId == orgId, ct);
        }
        else
        {
            return EventsHelpers.BadRequest("Provide either participantId or code.");
        }

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var method = (request.Method ?? "manual").Trim().ToLowerInvariant();
        if (method is not ("manual" or "qr"))
        {
            method = "manual";
        }

        var alreadyCheckedIn = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.ParticipantId == participant.Id && x.OrganizationId == orgId, ct);

        if (!alreadyCheckedIn)
        {
            db.CheckIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                EventId = id,
                ParticipantId = participant.Id,
                OrganizationId = orgId,
                CheckedInAt = DateTime.UtcNow,
                Method = method
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            { }
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInResponse(participant.Id, participant.FullName, alreadyCheckedIn, arrivedCount, totalCount));
    }

    internal static async Task<IResult> UndoCheckIn(
        string eventId,
        CheckInUndoRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await UndoCheckInForOrg(orgId, eventId, request, db, ct);
    }

    internal static async Task<IResult> UndoCheckInForOrg(
        Guid orgId,
        string eventId,
        CheckInUndoRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        ParticipantEntity? participant = null;

        if (request.ParticipantId.HasValue)
        {
            participant = await db.Participants.FirstOrDefaultAsync(
                x => x.Id == request.ParticipantId.Value && x.EventId == id && x.OrganizationId == orgId, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.CheckInCode))
        {
            var code = request.CheckInCode.Trim().ToUpperInvariant();
            if (code.Length != 8)
            {
                return EventsHelpers.BadRequest("Check-in code must be 8 characters.");
            }

            participant = await db.Participants.FirstOrDefaultAsync(
                x => x.EventId == id && x.CheckInCode == code && x.OrganizationId == orgId, ct);
        }
        else
        {
            return EventsHelpers.BadRequest("Provide participantId or checkInCode.");
        }

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var checkIn = await db.CheckIns.FirstOrDefaultAsync(
            x => x.EventId == id && x.ParticipantId == participant.Id && x.OrganizationId == orgId, ct);

        var alreadyUndone = checkIn is null;
        if (!alreadyUndone)
        {
            db.CheckIns.Remove(checkIn!);
            await db.SaveChangesAsync(ct);
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInUndoResponse(participant.Id, alreadyUndone, arrivedCount, totalCount));
    }

    internal static async Task<IResult> CheckInByCode(
        string eventId,
        CheckInCodeRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await CheckInByCodeForOrg(orgId, eventId, request, db, ct);
    }

    internal static async Task<IResult> CheckInByCodeForOrg(
        Guid orgId,
        string eventId,
        CheckInCodeRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var code = request.CheckInCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return EventsHelpers.BadRequest("Check-in code is required.");
        }

        if (code.Length != 8)
        {
            return EventsHelpers.BadRequest("Check-in code must be 8 characters.");
        }

        return await CheckInForOrg(orgId, eventId, new CheckInRequest(code, null, "qr"), db, ct);
    }

    internal static async Task<IResult> GetCheckInSummary(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInSummary(arrivedCount, totalCount));
    }
}
