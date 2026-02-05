using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Features.Portal;

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
                x.IsDeleted,
                x.EventAccessCode))
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
            EventAccessCode = await EventsHelpers.GenerateEventAccessCodeAsync(db, ct),
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
        db.EventDays.AddRange(EventsHelpers.CreateDefaultDays(entity));
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

    internal static async Task<IResult> GetEventDays(
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

        var eventEntity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (eventEntity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var days = await EnsureEventDaysAsync(eventEntity, db, ct);

        var activityCounts = await db.EventActivities.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .GroupBy(x => x.EventDayId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var result = days
            .OrderBy(x => x.SortOrder)
            .Select(day => new EventDayDto(
                day.Id,
                day.Date.ToString("yyyy-MM-dd"),
                day.Title,
                day.Notes,
                day.SortOrder,
                day.IsActive,
                activityCounts.TryGetValue(day.Id, out var count) ? count : 0))
            .ToArray();

        return Results.Ok(result);
    }

    internal static async Task<IResult> CreateEventDay(
        string eventId,
        CreateEventDayRequest request,
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

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        if (!EventsHelpers.TryParseDate(request.Date, out var date))
        {
            return EventsHelpers.BadRequest("Date must be in YYYY-MM-DD format.");
        }

        var sortOrder = request.SortOrder
            ?? await db.EventDays.AsNoTracking()
                .Where(x => x.EventId == id && x.OrganizationId == orgId)
                .Select(x => (int?)x.SortOrder)
                .MaxAsync(ct) + 1
            ?? 1;

        var entity = new EventDayEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EventId = id,
            Date = date,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SortOrder = sortOrder,
            IsActive = request.IsActive ?? true
        };

        db.EventDays.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new EventDayDto(
            entity.Id,
            entity.Date.ToString("yyyy-MM-dd"),
            entity.Title,
            entity.Notes,
            entity.SortOrder,
            entity.IsActive,
            0));
    }

    internal static async Task<IResult> UpdateEventDay(
        string eventId,
        string dayId,
        UpdateEventDayRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(dayId, out var dayGuid))
        {
            return EventsHelpers.BadRequest("Invalid day id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.EventDays.FirstOrDefaultAsync(
            x => x.Id == dayGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Day not found." });
        }

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        if (request.Date is not null)
        {
            if (!EventsHelpers.TryParseDate(request.Date, out var date))
            {
                return EventsHelpers.BadRequest("Date must be in YYYY-MM-DD format.");
            }
            entity.Date = date;
        }

        if (request.Title is not null)
        {
            entity.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        }

        if (request.Notes is not null)
        {
            entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        }

        if (request.SortOrder.HasValue)
        {
            entity.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        await db.SaveChangesAsync(ct);

        var activityCount = await db.EventActivities.AsNoTracking()
            .CountAsync(x => x.EventDayId == entity.Id && x.OrganizationId == orgId, ct);

        return Results.Ok(new EventDayDto(
            entity.Id,
            entity.Date.ToString("yyyy-MM-dd"),
            entity.Title,
            entity.Notes,
            entity.SortOrder,
            entity.IsActive,
            activityCount));
    }

    internal static async Task<IResult> DeleteEventDay(
        string eventId,
        string dayId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(dayId, out var dayGuid))
        {
            return EventsHelpers.BadRequest("Invalid day id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.EventDays.FirstOrDefaultAsync(
            x => x.Id == dayGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Day not found." });
        }

        db.EventDays.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    internal static async Task<IResult> GetEventActivities(
        string eventId,
        string dayId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(dayId, out var dayGuid))
        {
            return EventsHelpers.BadRequest("Invalid day id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var dayExists = await db.EventDays.AsNoTracking()
            .AnyAsync(x => x.Id == dayGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (!dayExists)
        {
            return Results.NotFound(new { message = "Day not found." });
        }

        var activities = await db.EventActivities.AsNoTracking()
            .Where(x => x.EventDayId == dayGuid && x.OrganizationId == orgId)
            .OrderBy(x => x.StartTime)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

        return Results.Ok(activities.Select(EventsHelpers.ToActivityDto).ToArray());
    }

    internal static async Task<IResult> CreateEventActivity(
        string eventId,
        string dayId,
        CreateEventActivityRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(dayId, out var dayGuid))
        {
            return EventsHelpers.BadRequest("Invalid day id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var day = await db.EventDays.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dayGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (day is null)
        {
            return Results.NotFound(new { message = "Day not found." });
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return EventsHelpers.BadRequest("Title is required.");
        }

        if (!EventsHelpers.TryParseOptionalTime(request.StartTime, out var startTime))
        {
            return EventsHelpers.BadRequest("Start time is invalid.");
        }

        if (!EventsHelpers.TryParseOptionalTime(request.EndTime, out var endTime))
        {
            return EventsHelpers.BadRequest("End time is invalid.");
        }

        if (startTime.HasValue && endTime.HasValue && endTime < startTime)
        {
            return EventsHelpers.BadRequest("End time must be after start time.");
        }

        var entity = new EventActivityEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EventId = eventGuid,
            EventDayId = dayGuid,
            Title = title,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Other" : request.Type.Trim(),
            StartTime = startTime,
            EndTime = endTime,
            LocationName = string.IsNullOrWhiteSpace(request.LocationName) ? null : request.LocationName.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Directions = string.IsNullOrWhiteSpace(request.Directions) ? null : request.Directions.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CheckInEnabled = request.CheckInEnabled ?? false,
            CheckInMode = string.IsNullOrWhiteSpace(request.CheckInMode) ? "EntryOnly" : request.CheckInMode.Trim(),
            MenuText = string.IsNullOrWhiteSpace(request.MenuText) ? null : request.MenuText.Trim(),
            SurveyUrl = string.IsNullOrWhiteSpace(request.SurveyUrl) ? null : request.SurveyUrl.Trim()
        };

        db.EventActivities.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Ok(EventsHelpers.ToActivityDto(entity));
    }

    internal static async Task<IResult> UpdateEventActivity(
        string eventId,
        string activityId,
        UpdateEventActivityRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(activityId, out var activityGuid))
        {
            return EventsHelpers.BadRequest("Invalid activity id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var entity = await db.EventActivities.FirstOrDefaultAsync(
            x => x.Id == activityGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Activity not found." });
        }

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return EventsHelpers.BadRequest("Title is required.");
            }
            entity.Title = title;
        }

        if (request.Type is not null)
        {
            entity.Type = string.IsNullOrWhiteSpace(request.Type) ? "Other" : request.Type.Trim();
        }

        if (request.StartTime is not null)
        {
            if (!EventsHelpers.TryParseOptionalTime(request.StartTime, out var startTime))
            {
                return EventsHelpers.BadRequest("Start time is invalid.");
            }
            entity.StartTime = startTime;
        }

        if (request.EndTime is not null)
        {
            if (!EventsHelpers.TryParseOptionalTime(request.EndTime, out var endTime))
            {
                return EventsHelpers.BadRequest("End time is invalid.");
            }
            entity.EndTime = endTime;
        }

        if (entity.StartTime.HasValue && entity.EndTime.HasValue && entity.EndTime < entity.StartTime)
        {
            return EventsHelpers.BadRequest("End time must be after start time.");
        }

        if (request.LocationName is not null)
        {
            entity.LocationName = string.IsNullOrWhiteSpace(request.LocationName) ? null : request.LocationName.Trim();
        }

        if (request.Address is not null)
        {
            entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        }

        if (request.Directions is not null)
        {
            entity.Directions = string.IsNullOrWhiteSpace(request.Directions) ? null : request.Directions.Trim();
        }

        if (request.Notes is not null)
        {
            entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        }

        if (request.CheckInEnabled.HasValue)
        {
            entity.CheckInEnabled = request.CheckInEnabled.Value;
        }

        if (request.CheckInMode is not null)
        {
            entity.CheckInMode = string.IsNullOrWhiteSpace(request.CheckInMode) ? "EntryOnly" : request.CheckInMode.Trim();
        }

        if (request.MenuText is not null)
        {
            entity.MenuText = string.IsNullOrWhiteSpace(request.MenuText) ? null : request.MenuText.Trim();
        }

        if (request.SurveyUrl is not null)
        {
            entity.SurveyUrl = string.IsNullOrWhiteSpace(request.SurveyUrl) ? null : request.SurveyUrl.Trim();
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(EventsHelpers.ToActivityDto(entity));
    }

    internal static async Task<IResult> DeleteEventActivity(
        string eventId,
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(activityId, out var activityGuid))
        {
            return EventsHelpers.BadRequest("Invalid activity id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.EventActivities.FirstOrDefaultAsync(
            x => x.Id == activityGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Activity not found." });
        }

        db.EventActivities.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    internal static async Task<EventScheduleDto> BuildScheduleAsync(
        Guid eventId,
        Guid orgId,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var days = await db.EventDays.AsNoTracking()
            .Where(x => x.EventId == eventId && x.OrganizationId == orgId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        if (days.Count == 0)
        {
            var eventEntity = await db.Events.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == eventId && x.OrganizationId == orgId, ct);
            if (eventEntity is null)
            {
                return new EventScheduleDto(Array.Empty<EventScheduleDayDto>());
            }

            var created = EventsHelpers.CreateDefaultDays(eventEntity);
            db.EventDays.AddRange(created);
            await db.SaveChangesAsync(ct);
            days = created;
        }

        var dayIds = days.Select(x => x.Id).ToArray();
        var activities = dayIds.Length == 0
            ? new List<EventActivityEntity>()
            : await db.EventActivities.AsNoTracking()
                .Where(x => x.EventId == eventId && x.OrganizationId == orgId && dayIds.Contains(x.EventDayId))
                .ToListAsync(ct);

        return EventsHelpers.ToScheduleDto(days, activities);
    }

    private static async Task<List<EventDayEntity>> EnsureEventDaysAsync(
        EventEntity eventEntity,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var days = await db.EventDays.AsNoTracking()
            .Where(x => x.EventId == eventEntity.Id && x.OrganizationId == eventEntity.OrganizationId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        if (days.Count > 0)
        {
            return days;
        }

        var created = EventsHelpers.CreateDefaultDays(eventEntity);
        db.EventDays.AddRange(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    internal static async Task<IResult> GetEvent(string eventId, HttpContext httpContext, TripflowDbContext db, CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var session = await PortalSessionHelpers.GetValidSessionAsync(httpContext, db, ct);
        EventEntity? entity = null;
        if (session is not null && session.EventId == id)
        {
            entity = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        }
        else
        {
            var role = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = string.Equals(role, "AgencyAdmin", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
            var isGuide = string.Equals(role, "Guide", StringComparison.OrdinalIgnoreCase);

            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            {
                return orgError!;
            }

            if (isAdmin)
            {
                entity = await db.Events.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
            }
            else if (isGuide)
            {
                var userId = httpContext.User.FindFirstValue("sub");
                if (!Guid.TryParse(userId, out var guideId))
                {
                    return Results.Unauthorized();
                }

                entity = await db.Events.AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == id && x.OrganizationId == orgId && x.GuideUserId == guideId,
                        ct);
            }
            else
            {
                return Results.Unauthorized();
            }
        }

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

    internal static async Task<IResult> GetEventAccessCode(
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

        return Results.Ok(new EventAccessCodeResponse(entity.Id, entity.EventAccessCode));
    }

    internal static async Task<IResult> RegenerateEventAccessCode(
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

        entity.EventAccessCode = await EventsHelpers.GenerateEventAccessCodeAsync(db, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new EventAccessCodeResponse(entity.Id, entity.EventAccessCode));
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
            var sessions = db.PortalSessions.Where(x => participantIds.Contains(x.ParticipantId));
            db.PortalSessions.RemoveRange(sessions);

            var details = db.ParticipantDetails.Where(x => participantIds.Contains(x.ParticipantId));
            db.ParticipantDetails.RemoveRange(details);
        }

        db.CheckIns.RemoveRange(db.CheckIns.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.EventActivities.RemoveRange(db.EventActivities.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.EventDays.RemoveRange(db.EventDays.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.EventPortals.RemoveRange(db.EventPortals.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.Participants.RemoveRange(db.Participants.Where(x => x.EventId == id && x.OrganizationId == orgId));
        db.Events.RemoveRange(db.Events.Where(x => x.Id == id && x.OrganizationId == orgId));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.NoContent();
    }

    internal static async Task<IResult> GetPortal(string eventId, HttpContext httpContext, TripflowDbContext db, CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var session = await PortalSessionHelpers.GetValidSessionAsync(httpContext, db, ct);
        EventEntity? eventEntity = null;
        if (session is not null && session.EventId == id)
        {
            eventEntity = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        }
        else
        {
            var role = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = string.Equals(role, "AgencyAdmin", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isAdmin)
            {
                return Results.Unauthorized();
            }

            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            {
                return orgError!;
            }

            eventEntity = await db.Events.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        }

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
            .Include(x => x.Details)
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
                row.FullName,
                row.Phone,
                row.Email,
                row.TcNo,
                row.BirthDate.ToString("yyyy-MM-dd"),
                row.Gender.ToString(),
                row.CheckInCode,
                row.Arrived,
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

    internal static async Task<IResult> GetParticipantsTable(
        string eventId,
        string? query,
        string? status,
        int? page,
        int? pageSize,
        string? sort,
        string? dir,
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

        var arrivedLookup = db.CheckIns.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .GroupBy(x => x.ParticipantId)
            .Select(g => new { ParticipantId = g.Key, ArrivedAt = g.Min(x => x.CheckedInAt) });

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId);

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            participantsQuery = participantsQuery.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || EF.Functions.ILike(x.TcNo, pattern)
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern))
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || EF.Functions.ILike(x.CheckInCode, pattern)
                || (x.Details != null && (
                    (x.Details.RoomNo != null && EF.Functions.ILike(x.Details.RoomNo, pattern))
                    || (x.Details.TicketNo != null && EF.Functions.ILike(x.Details.TicketNo, pattern))
                    || (x.Details.ArrivalPnr != null && EF.Functions.ILike(x.Details.ArrivalPnr, pattern))
                    || (x.Details.ReturnPnr != null && EF.Functions.ILike(x.Details.ReturnPnr, pattern))
                    || (x.Details.AgencyName != null && EF.Functions.ILike(x.Details.AgencyName, pattern))
                )));
        }

        var statusValue = (status ?? "all").Trim().ToLowerInvariant();
        if (statusValue is "arrived" or "not_arrived")
        {
            participantsQuery = statusValue == "arrived"
                ? participantsQuery.Where(x => db.CheckIns.Any(c =>
                    c.EventId == id && c.OrganizationId == orgId && c.ParticipantId == x.Id))
                : participantsQuery.Where(x => !db.CheckIns.Any(c =>
                    c.EventId == id && c.OrganizationId == orgId && c.ParticipantId == x.Id));
        }

        var total = await participantsQuery.CountAsync(ct);

        var baseQuery =
            from participant in participantsQuery
            join details in db.ParticipantDetails.AsNoTracking()
                on participant.Id equals details.ParticipantId into detailsJoin
            from details in detailsJoin.DefaultIfEmpty()
            join arrived in arrivedLookup
                on participant.Id equals arrived.ParticipantId into arrivedJoin
            from arrived in arrivedJoin.DefaultIfEmpty()
            select new { participant, details, arrivedAt = (DateTime?)arrived.ArrivedAt };

        var sortValue = (sort ?? "fullName").Trim().ToLowerInvariant();
        var dirValue = (dir ?? "asc").Trim().ToLowerInvariant();
        var descending = dirValue == "desc";

        baseQuery = sortValue switch
        {
            "arrivedat" => descending
                ? baseQuery.OrderByDescending(x => x.arrivedAt ?? DateTime.MinValue).ThenBy(x => x.participant.FullName)
                : baseQuery.OrderBy(x => x.arrivedAt ?? DateTime.MinValue).ThenBy(x => x.participant.FullName),
            "roomno" => descending
                ? baseQuery.OrderByDescending(x => x.details!.RoomNo ?? string.Empty).ThenBy(x => x.participant.FullName)
                : baseQuery.OrderBy(x => x.details!.RoomNo ?? string.Empty).ThenBy(x => x.participant.FullName),
            "agencyname" => descending
                ? baseQuery.OrderByDescending(x => x.details!.AgencyName ?? string.Empty).ThenBy(x => x.participant.FullName)
                : baseQuery.OrderBy(x => x.details!.AgencyName ?? string.Empty).ThenBy(x => x.participant.FullName),
            _ => descending
                ? baseQuery.OrderByDescending(x => x.participant.FullName)
                : baseQuery.OrderBy(x => x.participant.FullName)
        };

        var pageItems = await baseQuery
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var items = pageItems.Select(row => new ParticipantTableItemDto(
            row.participant.Id,
            row.participant.FullName,
            row.participant.Phone,
            row.participant.Email,
            row.participant.TcNo,
            row.participant.BirthDate.ToString("yyyy-MM-dd"),
            row.participant.Gender.ToString(),
            row.participant.CheckInCode,
            row.arrivedAt.HasValue,
            row.arrivedAt?.ToString("yyyy-MM-dd HH:mm"),
            row.details is null ? null : new ParticipantDetailsDto(
                row.details.RoomNo,
                row.details.RoomType,
                row.details.PersonNo,
                row.details.AgencyName,
                row.details.City,
                row.details.FlightCity,
                row.details.HotelCheckInDate?.ToString("yyyy-MM-dd"),
                row.details.HotelCheckOutDate?.ToString("yyyy-MM-dd"),
                row.details.TicketNo,
                row.details.AttendanceStatus,
                row.details.ArrivalAirline,
                row.details.ArrivalDepartureAirport,
                row.details.ArrivalArrivalAirport,
                row.details.ArrivalFlightCode,
                row.details.ArrivalDepartureTime?.ToString("HH:mm"),
                row.details.ArrivalArrivalTime?.ToString("HH:mm"),
                row.details.ArrivalPnr,
                row.details.ArrivalBaggageAllowance,
                row.details.ArrivalBaggagePieces,
                row.details.ArrivalBaggageTotalKg,
                row.details.ReturnAirline,
                row.details.ReturnDepartureAirport,
                row.details.ReturnArrivalAirport,
                row.details.ReturnFlightCode,
                row.details.ReturnDepartureTime?.ToString("HH:mm"),
                row.details.ReturnArrivalTime?.ToString("HH:mm"),
                row.details.ReturnPnr,
                row.details.ReturnBaggageAllowance,
                row.details.ReturnBaggagePieces,
                row.details.ReturnBaggageTotalKg)))
            .ToArray();

        return Results.Ok(new ParticipantTableResponseDto(
            resolvedPage,
            resolvedPageSize,
            total,
            items));
    }

    internal static async Task<IResult> GetParticipantProfile(
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

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        var participant = await db.Participants.AsNoTracking()
            .Include(x => x.Details)
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var arrivedAt = await db.CheckIns.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId && x.ParticipantId == participantGuid)
            .Select(x => (DateTime?)x.CheckedInAt)
            .FirstOrDefaultAsync(ct);

        var tcNoDuplicate = await db.Participants.AsNoTracking()
            .AnyAsync(x => x.EventId == id
                           && x.OrganizationId == orgId
                           && x.TcNo == participant.TcNo
                           && x.Id != participantGuid, ct);

        var dto = new ParticipantProfileDto(
            participant.Id,
            participant.FullName,
            participant.Phone,
            participant.Email,
            participant.TcNo,
            participant.BirthDate.ToString("yyyy-MM-dd"),
            participant.Gender.ToString(),
            participant.CheckInCode,
            arrivedAt.HasValue,
            arrivedAt?.ToString("yyyy-MM-dd HH:mm"),
            tcNoDuplicate,
            MapDetails(participant.Details));

        return Results.Ok(dto);
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

        var tcNo = NormalizeTcNo(request.TcNo);
        if (string.IsNullOrWhiteSpace(tcNo) || tcNo.Length != 11)
        {
            return EventsHelpers.BadRequest("TcNo must be 11 digits.");
        }

        if (!EventsHelpers.TryParseDate(request.BirthDate, out var birthDate))
        {
            return EventsHelpers.BadRequest("Birth date must be in YYYY-MM-DD format.");
        }

        if (string.IsNullOrWhiteSpace(request.Gender)
            || !Enum.TryParse<ParticipantGender>(request.Gender, true, out var gender))
        {
            return EventsHelpers.BadRequest("Gender is required.");
        }

        var tcExists = await db.Participants.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.OrganizationId == orgId && x.TcNo == tcNo, ct);
        if (tcExists)
        {
            httpContext.Response.Headers["X-Warning"] = "Duplicate TcNo exists for this event.";
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
            Phone = phone,
            Email = request.Email?.Trim(),
            TcNo = tcNo,
            BirthDate = birthDate,
            Gender = gender,
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
            new ParticipantDto(
                entity.Id,
                entity.FullName,
                entity.Phone,
                entity.Email,
                entity.TcNo,
                entity.BirthDate.ToString("yyyy-MM-dd"),
                entity.Gender.ToString(),
                entity.CheckInCode,
                false,
                MapDetails(entity.Details)));
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

        var entity = await db.Participants
            .Include(x => x.Details)
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
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

        var tcNo = NormalizeTcNo(request.TcNo);
        if (string.IsNullOrWhiteSpace(tcNo) || tcNo.Length != 11)
        {
            return EventsHelpers.BadRequest("TcNo must be 11 digits.");
        }

        if (!EventsHelpers.TryParseDate(request.BirthDate, out var birthDate))
        {
            return EventsHelpers.BadRequest("Birth date must be in YYYY-MM-DD format.");
        }

        if (string.IsNullOrWhiteSpace(request.Gender)
            || !Enum.TryParse<ParticipantGender>(request.Gender, true, out var gender))
        {
            return EventsHelpers.BadRequest("Gender is required.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (!string.Equals(entity.TcNo, tcNo, StringComparison.Ordinal))
        {
            var tcExists = await db.Participants.AsNoTracking()
                .AnyAsync(x => x.EventId == id && x.OrganizationId == orgId && x.TcNo == tcNo && x.Id != entity.Id, ct);
            if (tcExists)
            {
                httpContext.Response.Headers["X-Warning"] = "Duplicate TcNo exists for this event.";
            }
        }

        entity.FullName = fullName;
        entity.Email = email;
        entity.Phone = phone;
        entity.TcNo = tcNo;
        entity.BirthDate = birthDate;
        entity.Gender = gender;

        if (request.Details is not null)
        {
            if (entity.Details is null)
            {
                entity.Details = new ParticipantDetailsEntity
                {
                    ParticipantId = entity.Id
                };
            }

            if (!TryApplyDetails(entity.Details, request.Details, out var detailsError))
            {
                return EventsHelpers.BadRequest(detailsError);
            }
        }

        await db.SaveChangesAsync(ct);

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.ParticipantId == entity.Id && x.OrganizationId == orgId, ct);

        return Results.Ok(new ParticipantDto(
            entity.Id,
            entity.FullName,
            entity.Phone,
            entity.Email,
            entity.TcNo,
            entity.BirthDate.ToString("yyyy-MM-dd"),
            entity.Gender.ToString(),
            entity.CheckInCode,
            arrived,
            MapDetails(entity.Details)));
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

    internal static async Task<IResult> DeleteAllParticipants(
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

        var participants = await db.Participants
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .ToListAsync(ct);

        if (participants.Count == 0)
        {
            return Results.NoContent();
        }

        var participantIds = participants.Select(x => x.Id).ToArray();

        var checkIns = await db.CheckIns
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .ToListAsync(ct);
        if (checkIns.Count > 0)
        {
            db.CheckIns.RemoveRange(checkIns);
        }

        var portalSessions = await db.PortalSessions
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .ToListAsync(ct);
        if (portalSessions.Count > 0)
        {
            db.PortalSessions.RemoveRange(portalSessions);
        }

        db.Participants.RemoveRange(participants);
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

        var actorUserId = TryGetActorUserId(httpContext.User);
        var actorRole = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = null;
        }

        return await CheckInForOrg(orgId, eventId, request, actorUserId, actorRole, ipAddress, userAgent, db, ct);
    }

    internal static async Task<IResult> CheckInForOrg(
        Guid orgId,
        string eventId,
        CheckInRequest request,
        Guid? actorUserId,
        string? actorRole,
        string? ipAddress,
        string? userAgent,
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

        var direction = NormalizeLogDirection(request.Direction);
        var logMethod = NormalizeLogMethod(request.Method);
        var checkInMethod = logMethod == EventParticipantLogMethod.QrScan ? "qr" : "manual";
        var loggedAt = DateTime.UtcNow;

        ParticipantEntity? participant = null;
        var logResult = CheckInLogResults.Success;
        var alreadyCheckedIn = false;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
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
                logResult = CheckInLogResults.InvalidRequest;
            }

            if (logResult == CheckInLogResults.InvalidRequest)
            {
                db.EventParticipantLogs.Add(new EventParticipantLogEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    EventId = id,
                    ParticipantId = null,
                    Direction = direction,
                    Method = logMethod,
                    Result = logResult,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    ActorUserId = actorUserId,
                    ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
                    CreatedAt = loggedAt
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return Results.BadRequest(new
                {
                    message = "Provide either participantId or code.",
                    direction = direction.ToString(),
                    loggedAt,
                    result = logResult
                });
            }

            if (participant is null)
            {
                logResult = CheckInLogResults.NotFound;
                db.EventParticipantLogs.Add(new EventParticipantLogEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    EventId = id,
                    ParticipantId = null,
                    Direction = direction,
                    Method = logMethod,
                    Result = logResult,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    ActorUserId = actorUserId,
                    ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
                    CreatedAt = loggedAt
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return Results.NotFound(new
                {
                    message = "Participant not found.",
                    direction = direction.ToString(),
                    loggedAt,
                    result = logResult
                });
            }

            alreadyCheckedIn = await db.CheckIns.AsNoTracking()
                .AnyAsync(x => x.EventId == id && x.ParticipantId == participant.Id && x.OrganizationId == orgId, ct);

            if (direction == EventParticipantLogDirection.Entry)
            {
                if (alreadyCheckedIn)
                {
                    logResult = CheckInLogResults.AlreadyArrived;
                }
                else
                {
                    db.CheckIns.Add(new CheckInEntity
                    {
                        Id = Guid.NewGuid(),
                        EventId = id,
                        ParticipantId = participant.Id,
                        OrganizationId = orgId,
                        CheckedInAt = loggedAt,
                        Method = checkInMethod
                    });
                }
            }

            db.EventParticipantLogs.Add(new EventParticipantLogEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                EventId = id,
                ParticipantId = participant.Id,
                Direction = direction,
                Method = logMethod,
                Result = logResult,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ActorUserId = actorUserId,
                ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
                CreatedAt = loggedAt
            });

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (direction == EventParticipantLogDirection.Entry && IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();

            alreadyCheckedIn = true;
            logResult = CheckInLogResults.AlreadyArrived;

            await using var fallbackTransaction = await db.Database.BeginTransactionAsync(ct);
            db.EventParticipantLogs.Add(new EventParticipantLogEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                EventId = id,
                ParticipantId = participant?.Id,
                Direction = direction,
                Method = logMethod,
                Result = logResult,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ActorUserId = actorUserId,
                ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
                CreatedAt = loggedAt
            });
            await db.SaveChangesAsync(ct);
            await fallbackTransaction.CommitAsync(ct);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();

            try
            {
                await using var failureTransaction = await db.Database.BeginTransactionAsync(ct);
                db.EventParticipantLogs.Add(new EventParticipantLogEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    EventId = id,
                    ParticipantId = participant?.Id,
                    Direction = direction,
                    Method = logMethod,
                    Result = CheckInLogResults.Failed,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    ActorUserId = actorUserId,
                    ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
                    CreatedAt = loggedAt
                });
                await db.SaveChangesAsync(ct);
                await failureTransaction.CommitAsync(ct);
            }
            catch
            { }

            return Results.Json(new
            {
                message = "Request failed.",
                direction = direction.ToString(),
                loggedAt,
                result = CheckInLogResults.Failed
            }, statusCode: StatusCodes.Status500InternalServerError);
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInResponse(
            participant!.Id,
            participant.FullName,
            alreadyCheckedIn,
            arrivedCount,
            totalCount,
            direction.ToString(),
            loggedAt,
            logResult));
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

    internal static async Task<IResult> ResetAllCheckIns(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await ResetAllCheckInsForOrg(orgId, eventId, db, ct);
    }

    internal static async Task<IResult> ResetAllCheckInsForOrg(
        Guid orgId,
        string eventId,
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

        var checkIns = await db.CheckIns
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .ToListAsync(ct);

        var removedCount = checkIns.Count;
        if (removedCount > 0)
        {
            db.CheckIns.RemoveRange(checkIns);
            await db.SaveChangesAsync(ct);
        }

        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new ResetAllCheckInsResponse(removedCount, 0, totalCount));
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

        var actorUserId = TryGetActorUserId(httpContext.User);
        var actorRole = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = null;
        }

        return await CheckInByCodeForOrg(orgId, eventId, request, actorUserId, actorRole, ipAddress, userAgent, db, ct);
    }

    internal static async Task<IResult> CheckInByCodeForOrg(
        Guid orgId,
        string eventId,
        CheckInCodeRequest request,
        Guid? actorUserId,
        string? actorRole,
        string? ipAddress,
        string? userAgent,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        var direction = NormalizeLogDirection(request.Direction);

        var rawMethod = request.Method;
        if (string.IsNullOrWhiteSpace(rawMethod))
        {
            // Preserve legacy behavior: /checkins defaulted to qr when method is not provided.
            rawMethod = "QrScan";
        }
        var logMethod = NormalizeLogMethod(rawMethod);

        var code = NormalizeCheckInCode(request.CheckInCode ?? request.Code);
        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            var eventExists = await db.Events.AsNoTracking()
                .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
            if (!eventExists)
            {
                return Results.NotFound(new { message = "Event not found." });
            }

            var message = string.IsNullOrWhiteSpace(code)
                ? "Check-in code is required."
                : "Check-in code must be 8 characters.";
            var loggedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.EventParticipantLogs.Add(new EventParticipantLogEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                EventId = id,
                ParticipantId = null,
                Direction = direction,
                Method = logMethod,
                Result = CheckInLogResults.InvalidRequest,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ActorUserId = actorUserId,
                ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
                CreatedAt = loggedAt
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Results.BadRequest(new
            {
                message,
                direction = direction.ToString(),
                loggedAt,
                result = CheckInLogResults.InvalidRequest
            });
        }

        return await CheckInForOrg(
            orgId,
            eventId,
            new CheckInRequest(code, null, rawMethod, request.Direction),
            actorUserId,
            actorRole,
            ipAddress,
            userAgent,
            db,
            ct);
    }

    private static Guid? TryGetActorUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static EventParticipantLogDirection NormalizeLogDirection(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return EventParticipantLogDirection.Entry;
        }

        var normalized = new string(raw.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized == "exit" ? EventParticipantLogDirection.Exit : EventParticipantLogDirection.Entry;
    }

    private static EventParticipantLogMethod NormalizeLogMethod(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return EventParticipantLogMethod.Manual;
        }

        var normalized = new string(raw.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized switch
        {
            "qr" => EventParticipantLogMethod.QrScan,
            "qrscan" => EventParticipantLogMethod.QrScan,
            "scan" => EventParticipantLogMethod.QrScan,
            "manual" => EventParticipantLogMethod.Manual,
            _ => EventParticipantLogMethod.Manual
        };
    }

    private static string NormalizeCheckInCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var cleaned = raw.Trim().ToUpperInvariant();
        return new string(cleaned.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    private static class CheckInLogResults
    {
        internal const string Success = "Success";
        internal const string AlreadyArrived = "AlreadyArrived";
        internal const string NotFound = "NotFound";
        internal const string InvalidRequest = "InvalidRequest";
        internal const string Failed = "Failed";
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

    internal static async Task<IResult> GetEventParticipantLogs(
        string eventId,
        string? direction,
        string? method,
        string? result,
        string? from,
        string? to,
        string? query,
        int? page,
        int? pageSize,
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

        DateTime? fromUtc = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!TryParseLogFilterDate(from, out var parsedFrom, out var isDateOnly))
            {
                return EventsHelpers.BadRequest("Invalid from date.");
            }
            fromUtc = isDateOnly
                ? DateTime.SpecifyKind(parsedFrom.Date, DateTimeKind.Utc)
                : parsedFrom;
        }

        DateTime? toUtcExclusive = null;
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!TryParseLogFilterDate(to, out var parsedTo, out var isDateOnly))
            {
                return EventsHelpers.BadRequest("Invalid to date.");
            }
            toUtcExclusive = isDateOnly
                ? DateTime.SpecifyKind(parsedTo.Date, DateTimeKind.Utc).AddDays(1)
                : parsedTo;
        }

        var baseQuery =
            from log in db.EventParticipantLogs.AsNoTracking()
            where log.OrganizationId == orgId && log.EventId == id
            join participant in db.Participants.AsNoTracking()
                on log.ParticipantId equals participant.Id into participantJoin
            from participant in participantJoin.DefaultIfEmpty()
            join actor in db.Users.AsNoTracking()
                on log.ActorUserId equals actor.Id into actorJoin
            from actor in actorJoin.DefaultIfEmpty()
            select new { log, participant, actor };

        var directionValue = (direction ?? "all").Trim().ToLowerInvariant();
        if (directionValue == "entry")
        {
            baseQuery = baseQuery.Where(x => x.log.Direction == EventParticipantLogDirection.Entry);
        }
        else if (directionValue == "exit")
        {
            baseQuery = baseQuery.Where(x => x.log.Direction == EventParticipantLogDirection.Exit);
        }

        var methodValue = (method ?? "all").Trim();
        if (!string.Equals(methodValue, "all", StringComparison.OrdinalIgnoreCase))
        {
            var parsedMethod = TryParseLogMethodFilter(methodValue);
            if (!parsedMethod.HasValue)
            {
                return EventsHelpers.BadRequest("Invalid method filter.");
            }

            baseQuery = baseQuery.Where(x => x.log.Method == parsedMethod.Value);
        }

        var resultValue = (result ?? "all").Trim();
        if (!string.IsNullOrWhiteSpace(resultValue) && !string.Equals(resultValue, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedResult = NormalizeLogResult(resultValue);
            if (normalizedResult is not null)
            {
                baseQuery = baseQuery.Where(x => x.log.Result == normalizedResult);
            }
        }

        if (fromUtc.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.log.CreatedAt >= fromUtc.Value);
        }

        if (toUtcExclusive.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.log.CreatedAt < toUtcExclusive.Value);
        }

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            baseQuery = baseQuery.Where(x =>
                (x.participant != null && (
                    EF.Functions.ILike(x.participant.FullName, pattern)
                    || EF.Functions.ILike(x.participant.TcNo, pattern)
                    || (x.participant.Phone != null && EF.Functions.ILike(x.participant.Phone, pattern))
                    || (x.participant.Email != null && EF.Functions.ILike(x.participant.Email, pattern))
                    || EF.Functions.ILike(x.participant.CheckInCode, pattern)
                ))
                || (x.actor != null && EF.Functions.ILike(x.actor.Email, pattern))
                || (x.log.ActorRole != null && EF.Functions.ILike(x.log.ActorRole, pattern))
            );
        }

        var total = await baseQuery.CountAsync(ct);

        var pageItems = await baseQuery
            .OrderByDescending(x => x.log.CreatedAt)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var items = pageItems.Select(row => new EventParticipantLogItemDto(
            row.log.Id,
            row.log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            row.log.Direction.ToString(),
            row.log.Method.ToString(),
            row.log.Result,
            row.log.ParticipantId,
            row.participant?.FullName,
            row.participant?.TcNo,
            row.participant?.Phone,
            row.participant?.CheckInCode,
            row.log.ActorUserId,
            row.actor?.Email,
            row.log.ActorRole,
            row.log.IpAddress,
            row.log.UserAgent))
            .ToArray();

        return Results.Ok(new EventParticipantLogListResponseDto(resolvedPage, resolvedPageSize, total, items));
    }

    private static bool TryParseLogFilterDate(string raw, out DateTime valueUtc, out bool isDateOnly)
    {
        var trimmed = raw.Trim();
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            isDateOnly = true;
            valueUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return true;
        }

        isDateOnly = false;
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            valueUtc = dt;
            return true;
        }

        valueUtc = default;
        return false;
    }

    private static string? NormalizeLogResult(string raw)
    {
        var normalized = new string(raw.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized switch
        {
            "success" => CheckInLogResults.Success,
            "alreadyarrived" => CheckInLogResults.AlreadyArrived,
            "notfound" => CheckInLogResults.NotFound,
            "invalidrequest" => CheckInLogResults.InvalidRequest,
            "failed" => CheckInLogResults.Failed,
            _ => null
        };
    }

    private static EventParticipantLogMethod? TryParseLogMethodFilter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = new string(raw.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized switch
        {
            "manual" => EventParticipantLogMethod.Manual,
            "qr" => EventParticipantLogMethod.QrScan,
            "qrscan" => EventParticipantLogMethod.QrScan,
            "scan" => EventParticipantLogMethod.QrScan,
            _ => null
        };
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
            details.PersonNo,
            details.AgencyName,
            details.City,
            details.FlightCity,
            details.HotelCheckInDate?.ToString("yyyy-MM-dd"),
            details.HotelCheckOutDate?.ToString("yyyy-MM-dd"),
            details.TicketNo,
            details.AttendanceStatus,
            details.ArrivalAirline,
            details.ArrivalDepartureAirport,
            details.ArrivalArrivalAirport,
            details.ArrivalFlightCode,
            details.ArrivalDepartureTime?.ToString("HH:mm"),
            details.ArrivalArrivalTime?.ToString("HH:mm"),
            details.ArrivalPnr,
            details.ArrivalBaggageAllowance,
            details.ArrivalBaggagePieces,
            details.ArrivalBaggageTotalKg,
            details.ReturnAirline,
            details.ReturnDepartureAirport,
            details.ReturnArrivalAirport,
            details.ReturnFlightCode,
            details.ReturnDepartureTime?.ToString("HH:mm"),
            details.ReturnArrivalTime?.ToString("HH:mm"),
            details.ReturnPnr,
            details.ReturnBaggageAllowance,
            details.ReturnBaggagePieces,
            details.ReturnBaggageTotalKg);
    }

    private static bool TryApplyDetails(
        ParticipantDetailsEntity details,
        ParticipantDetailsRequest request,
        out string error)
    {
        details.RoomNo = request.RoomNo;
        details.RoomType = request.RoomType;
        details.PersonNo = request.PersonNo;
        details.AgencyName = request.AgencyName;
        details.City = request.City;
        details.FlightCity = request.FlightCity;
        if (!TryParseDateOnly(request.HotelCheckInDate, out var checkIn))
        {
            error = "Hotel check-in date must be in YYYY-MM-DD format.";
            return false;
        }
        if (!TryParseDateOnly(request.HotelCheckOutDate, out var checkOut))
        {
            error = "Hotel check-out date must be in YYYY-MM-DD format.";
            return false;
        }
        details.HotelCheckInDate = checkIn;
        details.HotelCheckOutDate = checkOut;
        details.TicketNo = request.TicketNo;
        details.AttendanceStatus = request.AttendanceStatus;
        details.ArrivalAirline = request.ArrivalAirline;
        details.ArrivalDepartureAirport = request.ArrivalDepartureAirport;
        details.ArrivalArrivalAirport = request.ArrivalArrivalAirport;
        details.ArrivalFlightCode = request.ArrivalFlightCode;
        if (!TryParseTimeOnly(request.ArrivalDepartureTime, out var arrivalDeparture))
        {
            error = "Arrival departure time must be in HH:mm format.";
            return false;
        }
        if (!TryParseTimeOnly(request.ArrivalArrivalTime, out var arrivalArrival))
        {
            error = "Arrival arrival time must be in HH:mm format.";
            return false;
        }
        details.ArrivalDepartureTime = arrivalDeparture;
        details.ArrivalArrivalTime = arrivalArrival;
        details.ArrivalPnr = request.ArrivalPnr;
        details.ArrivalBaggageAllowance = request.ArrivalBaggageAllowance;
        details.ArrivalBaggagePieces = request.ArrivalBaggagePieces;
        details.ArrivalBaggageTotalKg = request.ArrivalBaggageTotalKg;
        details.ReturnAirline = request.ReturnAirline;
        details.ReturnDepartureAirport = request.ReturnDepartureAirport;
        details.ReturnArrivalAirport = request.ReturnArrivalAirport;
        details.ReturnFlightCode = request.ReturnFlightCode;
        if (!TryParseTimeOnly(request.ReturnDepartureTime, out var returnDeparture))
        {
            error = "Return departure time must be in HH:mm format.";
            return false;
        }
        if (!TryParseTimeOnly(request.ReturnArrivalTime, out var returnArrival))
        {
            error = "Return arrival time must be in HH:mm format.";
            return false;
        }
        details.ReturnDepartureTime = returnDeparture;
        details.ReturnArrivalTime = returnArrival;
        details.ReturnPnr = request.ReturnPnr;
        details.ReturnBaggageAllowance = request.ReturnBaggageAllowance;
        details.ReturnBaggagePieces = request.ReturnBaggagePieces;
        details.ReturnBaggageTotalKg = request.ReturnBaggageTotalKg;
        error = string.Empty;
        return true;
    }

    private static bool TryParseDateOnly(string? value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            date = parsed;
            return true;
        }

        date = null;
        return false;
    }

    private static bool TryParseTimeOnly(string? value, out TimeOnly? time)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            time = null;
            return true;
        }

        if (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            || TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            time = parsed;
            return true;
        }

        time = null;
        return false;
    }

    private static string NormalizeTcNo(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
