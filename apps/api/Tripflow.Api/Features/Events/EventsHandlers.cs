using System.Globalization;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Helpers;
using Tripflow.Api.Features.Portal;

namespace Tripflow.Api.Features.Events;

internal static class EventsHandlers
{
    private const string DocTypeHotel = "hotel";
    private const string DocTypeInsurance = "insurance";
    private const string DocTypeTransfer = "transfer";
    private const string DocTypeCategorySystem = "system";
    private const string DocTypeCategoryAccommodation = "accommodation";
    private const string DocTypeCategoryCustom = "custom";

    private static readonly HashSet<string> AccommodationContentAllowedKeys = new(StringComparer.Ordinal)
    {
        "hotelName",
        "address",
        "phone",
        "checkInDate",
        "checkOutDate",
        "checkInNote",
        "checkOutNote"
    };

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
            .Include(x => x.EventGuides)
            .Where(x => x.OrganizationId == orgId && (showArchived || !x.IsDeleted))
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
                null))
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

        if (!EventsHelpers.TryNormalizeTimeZoneId(request.TimeZoneId, out var timeZoneId, out var timeZoneErrorCode))
        {
            return Results.BadRequest(new
            {
                code = timeZoneErrorCode,
                message = "Time zone is required and must be a valid IANA identifier."
            });
        }

        string eventAccessCode;
        if (!string.IsNullOrWhiteSpace(request.EventAccessCode))
        {
            var normalized = EventsHelpers.NormalizeEventCode(request.EventAccessCode);
            if (!EventsHelpers.IsValidEventCode(normalized))
            {
                return Results.BadRequest(new { code = "invalid_event_access_code_format" });
            }

            var exists = await db.Events.AsNoTracking()
                .AnyAsync(e => e.EventAccessCode == normalized, ct);
            if (exists)
            {
                return Results.Conflict(new { code = "event_access_code_taken" });
            }

            eventAccessCode = normalized;
        }
        else
        {
            eventAccessCode = await EventsHelpers.GenerateEventAccessCodeAsync(db, ct);
        }

        var entity = new EventEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            TimeZoneId = timeZoneId,
            EventAccessCode = eventAccessCode,
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
        db.EventDocTabs.AddRange(EventsHelpers.CreateDefaultDocTabs(entity, DateTime.UtcNow));
        db.EventDays.AddRange(EventsHelpers.CreateDefaultDays(entity));
        entity.Items.AddRange(EventsHelpers.CreateDefaultEventItems(entity, 1));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(new { code = "event_access_code_taken" });
        }

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

        if (!EventsHelpers.TryNormalizeTimeZoneId(request.TimeZoneId, out var timeZoneId, out var timeZoneErrorCode))
        {
            return Results.BadRequest(new
            {
                code = timeZoneErrorCode,
                message = "Time zone is required and must be a valid IANA identifier."
            });
        }

        var entity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        entity.Name = name;
        entity.StartDate = startDate;
        entity.EndDate = endDate;
        entity.TimeZoneId = timeZoneId;

        await db.SaveChangesAsync(ct);
        return Results.Ok(EventsHelpers.ToDto(entity));
    }

    internal static async Task<IResult> GetEventContacts(
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

        return Results.Ok(EventsHelpers.ToEventContactsDto(entity));
    }

    internal static async Task<IResult> UpdateEventContacts(
        string eventId,
        UpdateEventContactsRequest request,
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

        if (!EventsHelpers.TryNormalizeHttpsAbsoluteUrl(request.WhatsappGroupUrl, out var normalizedWhatsappGroupUrl))
        {
            return Results.BadRequest(new
            {
                code = "invalid_whatsapp_group_url",
                message = "whatsappGroupUrl must be an absolute https URL."
            });
        }

        var entity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        entity.GuideName = EventsHelpers.NormalizeContactText(request.GuideName);
        entity.GuidePhone = EventsHelpers.NormalizeContactText(request.GuidePhone);
        entity.LeaderName = EventsHelpers.NormalizeContactText(request.LeaderName);
        entity.LeaderPhone = EventsHelpers.NormalizeContactText(request.LeaderPhone);
        entity.EmergencyPhone = EventsHelpers.NormalizeContactText(request.EmergencyPhone);
        entity.WhatsappGroupUrl = normalizedWhatsappGroupUrl;

        await db.SaveChangesAsync(ct);
        return Results.Ok(EventsHelpers.ToEventContactsDto(entity));
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
                day.PlacesToVisit,
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
            Notes = RichTextSanitizer.Sanitize(request.Notes),
            PlacesToVisit = string.IsNullOrWhiteSpace(request.PlacesToVisit) ? null : request.PlacesToVisit.Trim(),
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
            entity.PlacesToVisit,
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
            entity.Notes = RichTextSanitizer.Sanitize(request.Notes);
        }

        if (request.PlacesToVisit is not null)
        {
            entity.PlacesToVisit = string.IsNullOrWhiteSpace(request.PlacesToVisit) ? null : request.PlacesToVisit.Trim();
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
            entity.PlacesToVisit,
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

    internal static async Task<IResult> GetActivitiesForCheckIn(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
            return error!;
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == eventGuid && x.OrganizationId == orgId, ct);
        if (!eventExists)
            return Results.NotFound(new { message = "Event not found." });

        var activities = await db.EventActivities.AsNoTracking()
            .Where(x => x.EventId == eventGuid && x.OrganizationId == orgId && x.RequiresCheckIn)
            .OrderBy(x => x.EventDayId)
            .ThenBy(x => x.StartTime)
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

        // Allow end < start (activity over midnight, e.g. 23:00–00:15)

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
            Notes = RichTextSanitizer.Sanitize(request.Notes),
            CheckInEnabled = request.CheckInEnabled ?? false,
            RequiresCheckIn = request.RequiresCheckIn ?? false,
            CheckInMode = string.IsNullOrWhiteSpace(request.CheckInMode) ? "EntryOnly" : request.CheckInMode.Trim(),
            MenuText = RichTextSanitizer.Sanitize(request.MenuText),
            ProgramContent = RichTextSanitizer.Sanitize(request.ProgramContent),
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
            var normalizedType = string.IsNullOrWhiteSpace(request.Type) ? "Other" : request.Type.Trim();
            if (MealMenuHelpers.IsMealActivity(entity.Type) && !MealMenuHelpers.IsMealActivity(normalizedType))
            {
                var hasMealConfig = await db.ActivityMealGroups.AsNoTracking()
                    .AnyAsync(x => x.OrganizationId == orgId && x.EventId == eventGuid && x.ActivityId == entity.Id, ct);
                if (hasMealConfig)
                {
                    return Results.Conflict(new { code = "meal_config_exists", message = "Meal groups exist for this activity." });
                }
            }

            entity.Type = normalizedType;
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

        // Allow end < start (activity over midnight, e.g. 23:00–00:15)

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
            entity.Notes = RichTextSanitizer.Sanitize(request.Notes);
        }

        if (request.CheckInEnabled.HasValue)
        {
            entity.CheckInEnabled = request.CheckInEnabled.Value;
        }

        if (request.RequiresCheckIn.HasValue)
        {
            entity.RequiresCheckIn = request.RequiresCheckIn.Value;
        }

        if (request.CheckInMode is not null)
        {
            entity.CheckInMode = string.IsNullOrWhiteSpace(request.CheckInMode) ? "EntryOnly" : request.CheckInMode.Trim();
        }

        if (request.MenuText is not null)
        {
            entity.MenuText = RichTextSanitizer.Sanitize(request.MenuText);
        }

        if (request.ProgramContent is not null)
        {
            entity.ProgramContent = RichTextSanitizer.Sanitize(request.ProgramContent);
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

        var role = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);
        var isAdmin = string.Equals(role, "AgencyAdmin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        var isGuide = string.Equals(role, "Guide", StringComparison.OrdinalIgnoreCase);

        EventEntity? entity = null;

        if (isAdmin)
        {
            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            {
                return orgError!;
            }

            entity = await db.Events.AsNoTracking()
                .Include(x => x.EventGuides)
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
                .Include(x => x.EventGuides)
                .FirstOrDefaultAsync(
                    x => x.Id == id && x.EventGuides.Any(g => g.GuideUserId == guideId) && !x.IsDeleted,
                    ct);
        }
        else
        {
            var session = await PortalSessionHelpers.GetValidSessionAsync(httpContext, db, ct);
            if (session is not null && session.EventId == id)
            {
                entity = await db.Events.AsNoTracking()
                    .Include(x => x.EventGuides)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
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

    internal static async Task<IResult> UpdateEventAccessCode(
        string eventId,
        UpdateEventAccessCodeRequest request,
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

        if (request is null || string.IsNullOrWhiteSpace(request.EventAccessCode))
        {
            return Results.BadRequest(new { code = "invalid_event_access_code_format" });
        }

        var normalized = EventsHelpers.NormalizeEventCode(request.EventAccessCode);
        if (!EventsHelpers.IsValidEventCode(normalized))
        {
            return Results.BadRequest(new { code = "invalid_event_access_code_format" });
        }

        var entity = await db.Events.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var takenByOther = await db.Events.AsNoTracking()
            .AnyAsync(e => e.EventAccessCode == normalized && e.Id != id, ct);
        if (takenByOther)
        {
            return Results.Conflict(new { code = "event_access_code_taken" });
        }

        entity.EventAccessCode = normalized;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(new { code = "event_access_code_taken" });
        }

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
        db.EventDocTabs.RemoveRange(db.EventDocTabs.Where(x => x.EventId == id && x.OrganizationId == orgId));
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
            var isGuide = string.Equals(role, "Guide", StringComparison.OrdinalIgnoreCase);

            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            {
                return orgError!;
            }

            if (isAdmin)
            {
                eventEntity = await db.Events.AsNoTracking()
                    .Include(x => x.EventGuides)
                    .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
            }
            else if (isGuide)
            {
                var userId = httpContext.User.FindFirstValue("sub");
                if (!Guid.TryParse(userId, out var guideId))
                {
                    return Results.Unauthorized();
                }

                eventEntity = await db.Events.AsNoTracking()
                    .Include(x => x.EventGuides)
                    .FirstOrDefaultAsync(
                        x => x.Id == id && x.OrganizationId == orgId && x.EventGuides.Any(g => g.GuideUserId == guideId),
                        ct);
            }
            else
            {
                return Results.Unauthorized();
            }
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

    internal static async Task<IResult> GetDocTabs(
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

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var tabs = await db.EventDocTabs.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var response = tabs.Select(MapDocTab).ToArray();
        return Results.Ok(response);
    }

    internal static async Task<IResult> CreateDocTab(
        string eventId,
        CreateEventDocTabRequest request,
        HttpContext httpContext,
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

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var validation = ValidateDocTabRequest(
            request.Title,
            request.Type,
            request.SortOrder,
            request.IsActive,
            request.Content,
            null,
            eventInfo.StartDate,
            eventInfo.EndDate,
            out var title,
            out var type,
            out var sortOrder,
            out var isActive,
            out var contentJson);
        if (validation is not null)
        {
            return validation;
        }

        var normalizedType = NormalizeDocType(type);
        if (IsSystemDocType(normalizedType))
        {
            return Results.Conflict(new
            {
                code = "system_doc_type_create_forbidden",
                message = "System doc tabs cannot be created manually."
            });
        }

        var entity = new EventDocTabEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EventId = id,
            Title = title!,
            Type = type!,
            SortOrder = sortOrder,
            IsActive = isActive,
            ContentJson = contentJson!,
            CreatedAt = DateTime.UtcNow
        };

        db.EventDocTabs.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/events/{id}/docs/tabs/{entity.Id}", MapDocTab(entity));
    }

    internal static async Task<IResult> UpdateDocTab(
        string eventId,
        string tabId,
        UpdateEventDocTabRequest request,
        HttpContext httpContext,
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

        if (!Guid.TryParse(tabId, out var docTabId))
        {
            return EventsHelpers.BadRequest("Doc tab id is invalid.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.EventDocTabs
            .FirstOrDefaultAsync(x => x.Id == docTabId && x.EventId == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Doc tab not found." });
        }

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var validation = ValidateDocTabRequest(
            request.Title,
            request.Type,
            request.SortOrder,
            request.IsActive,
            request.Content,
            entity,
            eventInfo.StartDate,
            eventInfo.EndDate,
            out var title,
            out var type,
            out var sortOrder,
            out var isActive,
            out var contentJson);
        if (validation is not null)
        {
            return validation;
        }

        var existingNormalizedType = NormalizeDocType(entity.Type);
        var nextNormalizedType = NormalizeDocType(type);
        var existingCategory = GetDocTypeCategory(existingNormalizedType);
        var nextCategory = GetDocTypeCategory(nextNormalizedType);

        if (existingCategory != nextCategory)
        {
            return Results.Conflict(new
            {
                code = "doc_type_change_forbidden",
                message = "Doc tab type category cannot be changed."
            });
        }

        if (IsSystemDocType(existingNormalizedType) && existingNormalizedType != nextNormalizedType)
        {
            return Results.Conflict(new
            {
                code = "doc_type_change_forbidden",
                message = "System doc tab type cannot be changed."
            });
        }

        entity.Title = title!;
        entity.Type = type!;
        entity.SortOrder = sortOrder;
        entity.IsActive = isActive;
        entity.ContentJson = contentJson!;

        await db.SaveChangesAsync(ct);
        return Results.Ok(MapDocTab(entity));
    }

    internal static async Task<IResult> DeleteDocTab(
        string eventId,
        string tabId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(tabId, out var docTabId))
        {
            return EventsHelpers.BadRequest("Doc tab id is invalid.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var entity = await db.EventDocTabs
            .FirstOrDefaultAsync(x => x.Id == docTabId && x.EventId == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Doc tab not found." });
        }

        if (IsSystemDocType(NormalizeDocType(entity.Type)))
        {
            return Results.Conflict(new
            {
                code = "system_doc_type_delete_forbidden",
                message = "System doc tabs cannot be deleted."
            });
        }

        db.EventDocTabs.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    internal static async Task<IResult> AssignGuides(
        string eventId,
        AssignGuidesRequest request,
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

        if (request is null || request.GuideUserIds is null)
        {
            return EventsHelpers.BadRequest("Guide user ids array is required.");
        }

        var eventEntity = await db.Events
            .Include(x => x.EventGuides)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (eventEntity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var guideIds = request.GuideUserIds.Where(g => g != Guid.Empty).Distinct().ToArray();

        // Validate all guides exist and belong to the organization through organization guide memberships.
        if (guideIds.Length > 0)
        {
            var validGuides = await db.OrganizationGuides.AsNoTracking()
                .Where(x => x.OrganizationId == orgId
                    && guideIds.Contains(x.GuideUserId)
                    && x.GuideUser.Role == "Guide")
                .Select(x => x.GuideUserId)
                .ToArrayAsync(ct);

            if (validGuides.Length != guideIds.Length)
            {
                return EventsHelpers.BadRequest("One or more guide users not found or do not belong to the organization.");
            }
        }

        // Remove existing guides
        db.EventGuides.RemoveRange(eventEntity.EventGuides);

        // Add new guides
        foreach (var guideId in guideIds)
        {
            eventEntity.EventGuides.Add(new EventGuideEntity
            {
                EventId = eventEntity.Id,
                GuideUserId = guideId
            });
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { eventId = id, guideUserIds = guideIds });
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

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
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
                        && (x.Result == CheckInLogResults.Success || x.Result == CheckInLogResults.AlreadyArrived));

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

    internal static async Task<IResult> GetParticipantsTable(
        string eventId,
        string? query,
        string? status,
        string? flightFilter,
        string? accommodationFilter,
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

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
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
                || EF.Functions.ILike(x.FirstName, pattern)
                || EF.Functions.ILike(x.LastName, pattern)
                || EF.Functions.ILike(x.TcNo, pattern)
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern))
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || EF.Functions.ILike(x.CheckInCode, pattern)
                || (x.Details != null && (
                    (x.Details.RoomNo != null && EF.Functions.ILike(x.Details.RoomNo, pattern))
                    || (x.Details.TicketNo != null && EF.Functions.ILike(x.Details.TicketNo, pattern))
                    || (x.Details.ArrivalTicketNo != null && EF.Functions.ILike(x.Details.ArrivalTicketNo, pattern))
                    || (x.Details.ReturnTicketNo != null && EF.Functions.ILike(x.Details.ReturnTicketNo, pattern))
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

        var accommodationFilterValue = accommodationFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(accommodationFilterValue))
        {
            if (string.Equals(accommodationFilterValue, "unassigned", StringComparison.OrdinalIgnoreCase))
            {
                participantsQuery = participantsQuery.Where(x => x.Details == null || x.Details.AccommodationDocTabId == null);
            }
            else if (Guid.TryParse(accommodationFilterValue, out var accommodationDocTabId))
            {
                participantsQuery = participantsQuery.Where(x => x.Details != null && x.Details.AccommodationDocTabId == accommodationDocTabId);
            }
        }

        var flightFilterValue = (flightFilter ?? "all").Trim().ToLowerInvariant();
        if (flightFilterValue is "no_flights" or "no_arrival" or "no_return")
        {
            participantsQuery = flightFilterValue switch
            {
                "no_flights" => participantsQuery.Where(x =>
                    !db.ParticipantFlightSegments.Any(segment =>
                        segment.EventId == id
                        && segment.OrganizationId == orgId
                        && segment.ParticipantId == x.Id)
                    && (x.Details == null || (
                        x.Details.ArrivalAirline == null
                        && x.Details.ArrivalDepartureAirport == null
                        && x.Details.ArrivalArrivalAirport == null
                        && x.Details.ArrivalFlightCode == null
                        && x.Details.ArrivalFlightDate == null
                        && x.Details.ArrivalDepartureTime == null
                        && x.Details.ArrivalArrivalTime == null
                        && x.Details.ArrivalPnr == null
                        && x.Details.ArrivalTicketNo == null
                        && x.Details.ArrivalBaggagePieces == null
                        && x.Details.ArrivalBaggageTotalKg == null
                        && x.Details.ArrivalCabinBaggage == null
                        && x.Details.ReturnAirline == null
                        && x.Details.ReturnDepartureAirport == null
                        && x.Details.ReturnArrivalAirport == null
                        && x.Details.ReturnFlightCode == null
                        && x.Details.ReturnFlightDate == null
                        && x.Details.ReturnDepartureTime == null
                        && x.Details.ReturnArrivalTime == null
                        && x.Details.ReturnPnr == null
                        && x.Details.ReturnTicketNo == null
                        && x.Details.ReturnBaggagePieces == null
                        && x.Details.ReturnBaggageTotalKg == null
                        && x.Details.ReturnCabinBaggage == null)))
                ,
                "no_arrival" => participantsQuery.Where(x =>
                    !db.ParticipantFlightSegments.Any(segment =>
                        segment.EventId == id
                        && segment.OrganizationId == orgId
                        && segment.ParticipantId == x.Id
                        && segment.Direction == ParticipantFlightSegmentDirection.Arrival)
                    && (x.Details == null || (
                        x.Details.ArrivalAirline == null
                        && x.Details.ArrivalDepartureAirport == null
                        && x.Details.ArrivalArrivalAirport == null
                        && x.Details.ArrivalFlightCode == null
                        && x.Details.ArrivalFlightDate == null
                        && x.Details.ArrivalDepartureTime == null
                        && x.Details.ArrivalArrivalTime == null
                        && x.Details.ArrivalPnr == null
                        && x.Details.ArrivalTicketNo == null
                        && x.Details.ArrivalBaggagePieces == null
                        && x.Details.ArrivalBaggageTotalKg == null
                        && x.Details.ArrivalCabinBaggage == null)))
                ,
                _ => participantsQuery.Where(x =>
                    !db.ParticipantFlightSegments.Any(segment =>
                        segment.EventId == id
                        && segment.OrganizationId == orgId
                        && segment.ParticipantId == x.Id
                        && segment.Direction == ParticipantFlightSegmentDirection.Return)
                    && (x.Details == null || (
                        x.Details.ReturnAirline == null
                        && x.Details.ReturnDepartureAirport == null
                        && x.Details.ReturnArrivalAirport == null
                        && x.Details.ReturnFlightCode == null
                        && x.Details.ReturnFlightDate == null
                        && x.Details.ReturnDepartureTime == null
                        && x.Details.ReturnArrivalTime == null
                        && x.Details.ReturnPnr == null
                        && x.Details.ReturnTicketNo == null
                        && x.Details.ReturnBaggagePieces == null
                        && x.Details.ReturnBaggageTotalKg == null
                        && x.Details.ReturnCabinBaggage == null)))
            };
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

        var pageParticipantIds = pageItems.Select(x => x.participant.Id).ToArray();
        var pageFlightDirections = await db.ParticipantFlightSegments.AsNoTracking()
            .Where(segment =>
                segment.EventId == id
                && segment.OrganizationId == orgId
                && pageParticipantIds.Contains(segment.ParticipantId))
            .Select(segment => new
            {
                segment.ParticipantId,
                segment.Direction
            })
            .Distinct()
            .ToListAsync(ct);

        var arrivalParticipantIds = pageFlightDirections
            .Where(x => x.Direction == ParticipantFlightSegmentDirection.Arrival)
            .Select(x => x.ParticipantId)
            .ToHashSet();
        var returnParticipantIds = pageFlightDirections
            .Where(x => x.Direction == ParticipantFlightSegmentDirection.Return)
            .Select(x => x.ParticipantId)
            .ToHashSet();

        var items = pageItems.Select(row => new ParticipantTableItemDto(
            row.participant.Id,
            row.participant.FirstName,
            row.participant.LastName,
            row.participant.FullName,
            row.participant.Phone,
            row.participant.Email,
            row.participant.TcNo,
            row.participant.BirthDate.ToString("yyyy-MM-dd"),
            row.participant.Gender.ToString(),
            row.participant.CheckInCode,
            row.arrivedAt.HasValue,
            row.arrivedAt?.ToString("yyyy-MM-dd HH:mm"),
            arrivalParticipantIds.Contains(row.participant.Id) || (row.details != null && (
                row.details.ArrivalAirline != null
                || row.details.ArrivalDepartureAirport != null
                || row.details.ArrivalArrivalAirport != null
                || row.details.ArrivalFlightCode != null
                || row.details.ArrivalFlightDate != null
                || row.details.ArrivalDepartureTime != null
                || row.details.ArrivalArrivalTime != null
                || row.details.ArrivalPnr != null
                || row.details.ArrivalTicketNo != null
                || row.details.ArrivalBaggagePieces != null
                || row.details.ArrivalBaggageTotalKg != null
                || row.details.ArrivalCabinBaggage != null)),
            returnParticipantIds.Contains(row.participant.Id) || (row.details != null && (
                row.details.ReturnAirline != null
                || row.details.ReturnDepartureAirport != null
                || row.details.ReturnArrivalAirport != null
                || row.details.ReturnFlightCode != null
                || row.details.ReturnFlightDate != null
                || row.details.ReturnDepartureTime != null
                || row.details.ReturnArrivalTime != null
                || row.details.ReturnPnr != null
                || row.details.ReturnTicketNo != null
                || row.details.ReturnBaggagePieces != null
                || row.details.ReturnBaggageTotalKg != null
                || row.details.ReturnCabinBaggage != null)),
            row.details is null ? null : new ParticipantDetailsDto(
                row.details.RoomNo,
                row.details.RoomType,
                row.details.BoardType,
                row.details.PersonNo,
                row.details.AgencyName,
                row.details.City,
                row.details.FlightCity,
                row.details.HotelCheckInDate?.ToString("yyyy-MM-dd"),
                row.details.HotelCheckOutDate?.ToString("yyyy-MM-dd"),
                row.details.ArrivalTicketNo ?? row.details.TicketNo,
                row.details.ArrivalTicketNo ?? row.details.TicketNo,
                row.details.ReturnTicketNo,
                row.details.AttendanceStatus,
                row.details.InsuranceCompanyName,
                row.details.InsurancePolicyNo,
                row.details.InsuranceStartDate?.ToString("yyyy-MM-dd"),
                row.details.InsuranceEndDate?.ToString("yyyy-MM-dd"),
                row.details.ArrivalAirline,
                row.details.ArrivalDepartureAirport,
                row.details.ArrivalArrivalAirport,
                row.details.ArrivalFlightCode,
                row.details.ArrivalFlightDate?.ToString("yyyy-MM-dd"),
                row.details.ArrivalDepartureTime?.ToString("HH:mm"),
                row.details.ArrivalArrivalTime?.ToString("HH:mm"),
                row.details.ArrivalPnr,
                row.details.ArrivalBaggageAllowance,
                row.details.ArrivalBaggagePieces,
                row.details.ArrivalBaggageTotalKg,
                row.details.ArrivalCabinBaggage,
                row.details.ReturnAirline,
                row.details.ReturnDepartureAirport,
                row.details.ReturnArrivalAirport,
                row.details.ReturnFlightCode,
                row.details.ReturnFlightDate?.ToString("yyyy-MM-dd"),
                row.details.ReturnDepartureTime?.ToString("HH:mm"),
                row.details.ReturnArrivalTime?.ToString("HH:mm"),
                row.details.ReturnPnr,
                row.details.ReturnBaggageAllowance,
                row.details.ReturnBaggagePieces,
                row.details.ReturnBaggageTotalKg,
                row.details.ReturnCabinBaggage,
                row.details.ArrivalTransferPickupTime?.ToString("HH:mm"),
                row.details.ArrivalTransferPickupPlace,
                row.details.ArrivalTransferDropoffPlace,
                row.details.ArrivalTransferVehicle,
                row.details.ArrivalTransferPlate,
                row.details.ArrivalTransferDriverInfo,
                row.details.ArrivalTransferNote,
                row.details.ReturnTransferPickupTime?.ToString("HH:mm"),
                row.details.ReturnTransferPickupPlace,
                row.details.ReturnTransferDropoffPlace,
                row.details.ReturnTransferVehicle,
                row.details.ReturnTransferPlate,
                row.details.ReturnTransferDriverInfo,
                row.details.ReturnTransferNote,
                row.details.AccommodationDocTabId)))
            .ToArray();

        return Results.Ok(new ParticipantTableResponseDto(
            resolvedPage,
            resolvedPageSize,
            total,
            items));
    }

    internal static async Task<IResult> BulkApplyParticipantRooms(
        string eventId,
        BulkApplyParticipantRoomsRequest request,
        HttpContext httpContext,
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

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var overwriteMode = NormalizeRoomOverwriteMode(request.OverwriteMode);
        if (overwriteMode is null)
        {
            return Results.BadRequest(new
            {
                code = "invalid_room_overwrite_mode",
                message = "overwriteMode must be always or only_empty."
            });
        }

        var onlyEmpty = overwriteMode == "only_empty";
        var errors = new List<BulkApplyParticipantRoomsErrorDto>();
        var notFoundTcNoCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var affectedCount = 0;

        var accommodationValidationCache = new Dictionary<Guid, AccommodationDocValidationResult>();

        async Task<(bool hasPatch, Guid? normalizedValue, IResult? validationError)> ResolveAccommodationPatch(Guid? rawValue)
        {
            if (!rawValue.HasValue)
            {
                return (false, null, null);
            }

            if (rawValue.Value == Guid.Empty)
            {
                return (true, null, null);
            }

            if (!accommodationValidationCache.TryGetValue(rawValue.Value, out var cached))
            {
                cached = await ValidateAccommodationDocReference(id, orgId, rawValue.Value, db, ct);
                accommodationValidationCache[rawValue.Value] = cached;
            }

            return (true, cached.Value, cached.Error);
        }

        var hasRowUpdates = request.RowUpdates is { Length: > 0 };

        if (hasRowUpdates)
        {
            var rowUpdates = request.RowUpdates!;
            affectedCount = rowUpdates.Length;

            var participantIds = rowUpdates
                .Select(x => x.ParticipantId)
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToArray();

            var participantsById = await db.Participants
                .Include(x => x.Details)
                .Where(x => x.EventId == id && x.OrganizationId == orgId && participantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var row in rowUpdates)
            {
                if (!participantsById.TryGetValue(row.ParticipantId, out var participant))
                {
                    skippedCount++;
                    if (!string.IsNullOrWhiteSpace(row.TcNo))
                    {
                        notFoundTcNoCount++;
                    }

                    errors.Add(new BulkApplyParticipantRoomsErrorDto(
                        row.ParticipantId,
                        row.TcNo,
                        "participant_not_found",
                        "Participant could not be resolved for room update."));
                    continue;
                }

                var patch = row.Patch ?? request.Patch;
                if (!HasAnyRoomPatchField(patch))
                {
                    skippedCount++;
                    continue;
                }

                var accommodationResolve = await ResolveAccommodationPatch(patch!.AccommodationDocTabId);
                if (accommodationResolve.validationError is not null)
                {
                    skippedCount++;
                    errors.Add(new BulkApplyParticipantRoomsErrorDto(
                        participant.Id,
                        row.TcNo ?? participant.TcNo,
                        "invalid_accommodation_doc_tab_id",
                        "Accommodation doc tab id must belong to this event and must be of type Hotel."));
                    continue;
                }

                participant.Details ??= new ParticipantDetailsEntity { ParticipantId = participant.Id };

                if (!TryApplyRoomPatch(
                        participant.Details,
                        patch!,
                        onlyEmpty,
                        accommodationResolve.hasPatch,
                        accommodationResolve.normalizedValue,
                        eventInfo.StartDate,
                        eventInfo.EndDate,
                        out var patchErrorCode,
                        out var patchErrorMessage,
                        out var changed))
                {
                    skippedCount++;
                    errors.Add(new BulkApplyParticipantRoomsErrorDto(
                        participant.Id,
                        row.TcNo ?? participant.TcNo,
                        patchErrorCode,
                        patchErrorMessage));
                    continue;
                }

                if (changed)
                {
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
        }
        else
        {
            if (!HasAnyRoomPatchField(request.Patch))
            {
                return Results.BadRequest(new
                {
                    code = "invalid_room_patch",
                    message = "patch or rowUpdates is required."
                });
            }

            var scope = NormalizeRoomScope(request.Scope);
            if (scope is null)
            {
                return Results.BadRequest(new
                {
                    code = "invalid_room_scope",
                    message = "scope must be manual, filtered, or all_event."
                });
            }

            IQueryable<ParticipantEntity> participantsQuery = db.Participants
                .Include(x => x.Details)
                .Where(x => x.EventId == id && x.OrganizationId == orgId);

            if (scope == "manual")
            {
                var participantIds = (request.ParticipantIds ?? Array.Empty<Guid>())
                    .Where(x => x != Guid.Empty)
                    .Distinct()
                    .ToArray();
                if (participantIds.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        code = "invalid_room_scope",
                        message = "participantIds is required when scope is manual."
                    });
                }

                participantsQuery = participantsQuery.Where(x => participantIds.Contains(x.Id));
            }
            else if (scope == "filtered")
            {
                participantsQuery = ApplyRoomFilters(participantsQuery, request.Filters, id, orgId, db);
            }

            var participants = await participantsQuery.ToListAsync(ct);
            affectedCount = participants.Count;

            var patch = request.Patch!;
            var accommodationResolve = await ResolveAccommodationPatch(patch.AccommodationDocTabId);
            if (accommodationResolve.validationError is not null)
            {
                return accommodationResolve.validationError;
            }

            foreach (var participant in participants)
            {
                participant.Details ??= new ParticipantDetailsEntity { ParticipantId = participant.Id };

                if (!TryApplyRoomPatch(
                        participant.Details,
                        patch,
                        onlyEmpty,
                        accommodationResolve.hasPatch,
                        accommodationResolve.normalizedValue,
                        eventInfo.StartDate,
                        eventInfo.EndDate,
                        out var patchErrorCode,
                        out var patchErrorMessage,
                        out var changed))
                {
                    skippedCount++;
                    errors.Add(new BulkApplyParticipantRoomsErrorDto(
                        participant.Id,
                        participant.TcNo,
                        patchErrorCode,
                        patchErrorMessage));
                    continue;
                }

                if (changed)
                {
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
        }

        if (updatedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new BulkApplyParticipantRoomsResponse(
            affectedCount,
            updatedCount,
            skippedCount,
            notFoundTcNoCount,
            errors.ToArray()));
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
            .Include(x => x.FlightSegments)
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
            participant.FirstName,
            participant.LastName,
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
            MapDetails(participant.Details),
            MapFlightSegments(participant.FlightSegments, ParticipantFlightSegmentDirection.Arrival),
            MapFlightSegments(participant.FlightSegments, ParticipantFlightSegmentDirection.Return));

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

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var firstName = NormalizeName(request.FirstName);
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return EventsHelpers.BadRequest("First name is required.");
        }

        var lastName = NormalizeName(request.LastName);
        if (string.IsNullOrWhiteSpace(lastName))
        {
            return EventsHelpers.BadRequest("Last name is required.");
        }

        var fullName = BuildFullName(firstName, lastName);

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
            FirstName = firstName,
            LastName = lastName,
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
                entity.FirstName,
                entity.LastName,
                entity.FullName,
                entity.Phone,
                entity.Email,
                entity.TcNo,
                entity.BirthDate.ToString("yyyy-MM-dd"),
                entity.Gender.ToString(),
                entity.CheckInCode,
                false,
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

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
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

        var firstName = NormalizeName(request.FirstName);
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return EventsHelpers.BadRequest("First name is required.");
        }

        var lastName = NormalizeName(request.LastName);
        if (string.IsNullOrWhiteSpace(lastName))
        {
            return EventsHelpers.BadRequest("Last name is required.");
        }

        var fullName = BuildFullName(firstName, lastName);

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

        entity.FirstName = firstName;
        entity.LastName = lastName;
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

            var detailsValidation = await ValidateAccommodationDocReference(
                id,
                orgId,
                request.Details.AccommodationDocTabId,
                db,
                ct);
            if (detailsValidation.Error is not null)
            {
                return detailsValidation.Error;
            }

            if (!TryApplyDetails(
                    entity.Details,
                    request.Details,
                    detailsValidation.Value,
                    eventInfo.StartDate,
                    eventInfo.EndDate,
                    out var detailsError))
            {
                return EventsHelpers.BadRequest(detailsError);
            }
        }

        await db.SaveChangesAsync(ct);

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.ParticipantId == entity.Id && x.OrganizationId == orgId, ct);

        return Results.Ok(new ParticipantDto(
            entity.Id,
            entity.FirstName,
            entity.LastName,
            entity.FullName,
            entity.Phone,
            entity.Email,
            entity.TcNo,
            entity.BirthDate.ToString("yyyy-MM-dd"),
            entity.Gender.ToString(),
            entity.CheckInCode,
            arrived,
            entity.WillNotAttend,
            MapDetails(entity.Details)));
    }

    internal static async Task<IResult> ReplaceParticipantFlights(
        string eventId,
        string participantId,
        ReplaceParticipantFlightsRequest request,
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

        var participant = await db.Participants
            .Include(x => x.FlightSegments)
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);
        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var arrivalRequest = request.ArrivalSegments ?? Array.Empty<FlightSegmentDto>();
        var returnRequest = request.ReturnSegments ?? Array.Empty<FlightSegmentDto>();

        if (!TryNormalizeFlightSegments(
                orgId,
                id,
                participant.Id,
                arrivalRequest,
                ParticipantFlightSegmentDirection.Arrival,
                out var arrivalSegments,
                out var arrivalError))
        {
            return EventsHelpers.BadRequest(arrivalError);
        }

        if (!TryNormalizeFlightSegments(
                orgId,
                id,
                participant.Id,
                returnRequest,
                ParticipantFlightSegmentDirection.Return,
                out var returnSegments,
                out var returnError))
        {
            return EventsHelpers.BadRequest(returnError);
        }

        if (participant.FlightSegments.Count > 0)
        {
            db.ParticipantFlightSegments.RemoveRange(participant.FlightSegments);
        }

        var replacement = new List<ParticipantFlightSegmentEntity>(arrivalSegments.Count + returnSegments.Count);
        replacement.AddRange(arrivalSegments);
        replacement.AddRange(returnSegments);

        if (replacement.Count > 0)
        {
            db.ParticipantFlightSegments.AddRange(replacement);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ParticipantFlightsResponse(
            MapFlightSegments(replacement, ParticipantFlightSegmentDirection.Arrival),
            MapFlightSegments(replacement, ParticipantFlightSegmentDirection.Return)));
    }

    internal static async Task<IResult> BulkApplyFlightSegments(
        string eventId,
        BulkApplyFlightSegmentsRequest request,
        HttpContext httpContext,
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

        var participantIds = (request.ParticipantIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (participantIds.Length == 0)
        {
            return EventsHelpers.BadRequest(
                "participant_ids_required",
                "participantIds",
                "At least one participant must be selected.");
        }

        var participantCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.OrganizationId == orgId && x.EventId == id && participantIds.Contains(x.Id), ct);
        if (participantCount != participantIds.Length)
        {
            return EventsHelpers.BadRequest(
                "participants_not_in_event",
                "participantIds",
                "One or more participants were not found in this event.");
        }

        var requestedDirections = request.ApplyDirections ?? Array.Empty<string>();
        if (requestedDirections.Length == 0)
        {
            return EventsHelpers.BadRequest(
                "apply_directions_required",
                "applyDirections",
                "At least one direction must be selected.");
        }

        var directions = new List<ParticipantFlightSegmentDirection>();
        for (var i = 0; i < requestedDirections.Length; i++)
        {
            var value = requestedDirections[i]?.Trim();
            if (!TryParseFlightSegmentDirection(value, out var parsedDirection))
            {
                return EventsHelpers.BadRequest(
                    "invalid_apply_direction",
                    $"applyDirections[{i}]",
                    "Direction must be Arrival or Return.");
            }

            if (!directions.Contains(parsedDirection))
            {
                directions.Add(parsedDirection);
            }
        }

        if (!string.Equals(request.ReplaceMode?.Trim(), "ReplaceDirection", StringComparison.OrdinalIgnoreCase))
        {
            return EventsHelpers.BadRequest(
                "invalid_replace_mode",
                "replaceMode",
                "replaceMode must be ReplaceDirection.");
        }

        var segmentRoot = request.Segments;
        if (segmentRoot is null)
        {
            return EventsHelpers.BadRequest(
                "segments_required_for_direction",
                "segments",
                "Segments are required for the requested directions.");
        }

        var normalizedByDirection = new Dictionary<ParticipantFlightSegmentDirection, List<ParticipantFlightSegmentEntity>>();
        foreach (var direction in directions)
        {
            var source = direction == ParticipantFlightSegmentDirection.Arrival
                ? segmentRoot.Arrival ?? Array.Empty<FlightSegmentDto>()
                : segmentRoot.Return ?? Array.Empty<FlightSegmentDto>();

            if (source.Length == 0)
            {
                return EventsHelpers.BadRequest(
                    "segments_required_for_direction",
                    $"segments.{direction}",
                    $"At least one segment is required for {direction}.");
            }

            if (!TryNormalizeFlightSegments(
                    orgId,
                    id,
                    Guid.Empty,
                    source,
                    direction,
                    out var normalized,
                    out _,
                    out var validationError,
                    uppercaseAirports: true,
                    fieldPrefix: $"segments.{direction}"))
            {
                return EventsHelpers.BadRequest(
                    validationError!.Code,
                    validationError.Field,
                    validationError.Message);
            }

            if (normalized.Count == 0)
            {
                return EventsHelpers.BadRequest(
                    "segments_required_for_direction",
                    $"segments.{direction}",
                    $"At least one segment is required for {direction}.");
            }

            normalizedByDirection[direction] = normalized;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var existingSegments = await db.ParticipantFlightSegments
            .Where(x => x.OrganizationId == orgId
                        && x.EventId == id
                        && participantIds.Contains(x.ParticipantId)
                        && directions.Contains(x.Direction))
            .ToListAsync(ct);

        if (existingSegments.Count > 0)
        {
            db.ParticipantFlightSegments.RemoveRange(existingSegments);
        }

        var replacements = new List<ParticipantFlightSegmentEntity>();
        foreach (var participantIdValue in participantIds)
        {
            foreach (var direction in directions)
            {
                var template = normalizedByDirection[direction];
                for (var i = 0; i < template.Count; i++)
                {
                    var row = template[i];
                    replacements.Add(new ParticipantFlightSegmentEntity
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = orgId,
                        EventId = id,
                        ParticipantId = participantIdValue,
                        Direction = direction,
                        SegmentIndex = i + 1,
                        Airline = row.Airline,
                        DepartureAirport = row.DepartureAirport,
                        ArrivalAirport = row.ArrivalAirport,
                        FlightCode = row.FlightCode,
                        DepartureDate = row.DepartureDate,
                        DepartureTime = row.DepartureTime,
                        ArrivalDate = row.ArrivalDate,
                        ArrivalTime = row.ArrivalTime,
                        Pnr = row.Pnr,
                        TicketNo = row.TicketNo,
                        BaggagePieces = row.BaggagePieces,
                        BaggageTotalKg = row.BaggageTotalKg,
                        CabinBaggage = row.CabinBaggage,
                    });
                }
            }
        }

        if (replacements.Count > 0)
        {
            db.ParticipantFlightSegments.AddRange(replacements);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Results.Ok(new BulkApplyFlightSegmentsResponse(
            participantIds.Length,
            new BulkApplyFlightSegmentsAppliedDto(
                normalizedByDirection.TryGetValue(ParticipantFlightSegmentDirection.Arrival, out var arrivalApplied)
                    ? arrivalApplied.Count
                    : null,
                normalizedByDirection.TryGetValue(ParticipantFlightSegmentDirection.Return, out var returnApplied)
                    ? returnApplied.Count
                    : null)));
    }

    internal static async Task<IResult> SetParticipantWillNotAttend(
        string eventId,
        string participantId,
        ParticipantWillNotAttendRequest request,
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

        if (request is null || !request.WillNotAttend.HasValue)
        {
            return EventsHelpers.BadRequest("willNotAttend is required.");
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
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        entity.WillNotAttend = request.WillNotAttend.Value;
        await db.SaveChangesAsync(ct);

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.EventId == id && x.ParticipantId == entity.Id && x.OrganizationId == orgId, ct);

        var lastLog = await db.EventParticipantLogs.AsNoTracking()
            .Where(x => x.OrganizationId == orgId
                        && x.EventId == id
                        && x.ParticipantId == entity.Id
                        && (x.Result == CheckInLogResults.Success || x.Result == CheckInLogResults.AlreadyArrived))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ParticipantLastLogDto(
                x.Direction.ToString(),
                x.Method.ToString(),
                x.Result,
                x.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new ParticipantWillNotAttendResponseDto(
            entity.Id,
            entity.WillNotAttend,
            arrived,
            lastLog));
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

            if (direction == EventParticipantLogDirection.Entry && participant.WillNotAttend)
            {
                logResult = CheckInLogResults.InvalidRequest;
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

                return Results.Conflict(new
                {
                    code = "will_not_attend",
                    message = "Participant is marked as will-not-attend.",
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

        var (arrivedCount, totalCount) = await GetCheckInCountsAsync(db, id, orgId, ct);

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

        var (arrivedCount, totalCount) = await GetCheckInCountsAsync(db, id, orgId, ct);

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

        var (arrivedCount, totalCount) = await GetCheckInCountsAsync(db, id, orgId, ct);
        return Results.Ok(new ResetAllCheckInsResponse(removedCount, arrivedCount, totalCount));
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

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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

    private static async Task<(int ArrivedCount, int TotalCount)> GetCheckInCountsAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        CancellationToken ct)
    {
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == eventId && x.OrganizationId == organizationId, ct);

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.EventId == eventId && x.OrganizationId == organizationId, ct);

        return (arrivedCount, totalCount);
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

        var (arrivedCount, totalCount) = await GetCheckInCountsAsync(db, id, orgId, ct);

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
                    || EF.Functions.ILike(x.participant.FirstName, pattern)
                    || EF.Functions.ILike(x.participant.LastName, pattern)
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

        var sortVal = (sort ?? "createdAt").Trim().ToLowerInvariant();
        var desc = (dir ?? "desc").Trim().ToLowerInvariant() == "desc";
        var ordered = sortVal switch
        {
            "participantname" => desc
                ? baseQuery.OrderByDescending(x => x.participant != null ? x.participant.FullName ?? "" : "").ThenByDescending(x => x.log.CreatedAt)
                : baseQuery.OrderBy(x => x.participant != null ? x.participant.FullName ?? "" : "").ThenBy(x => x.log.CreatedAt),
            "actoremail" => desc
                ? baseQuery.OrderByDescending(x => x.actor != null ? x.actor.Email ?? "" : "").ThenByDescending(x => x.log.CreatedAt)
                : baseQuery.OrderBy(x => x.actor != null ? x.actor.Email ?? "" : "").ThenBy(x => x.log.CreatedAt),
            "direction" => desc
                ? baseQuery.OrderByDescending(x => x.log.Direction.ToString()).ThenByDescending(x => x.log.CreatedAt)
                : baseQuery.OrderBy(x => x.log.Direction.ToString()).ThenBy(x => x.log.CreatedAt),
            "method" => desc
                ? baseQuery.OrderByDescending(x => x.log.Method.ToString()).ThenByDescending(x => x.log.CreatedAt)
                : baseQuery.OrderBy(x => x.log.Method.ToString()).ThenBy(x => x.log.CreatedAt),
            "result" => desc
                ? baseQuery.OrderByDescending(x => x.log.Result ?? "").ThenByDescending(x => x.log.CreatedAt)
                : baseQuery.OrderBy(x => x.log.Result ?? "").ThenBy(x => x.log.CreatedAt),
            _ => desc
                ? baseQuery.OrderByDescending(x => x.log.CreatedAt)
                : baseQuery.OrderBy(x => x.log.CreatedAt)
        };

        var pageItems = await ordered
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var items = pageItems.Select(row => new EventParticipantLogItemDto(
            row.log.Id,
            DateTime.SpecifyKind(row.log.CreatedAt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
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

    private static string? NormalizeRoomScope(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => "manual",
            "manual" => "manual",
            "filtered" => "filtered",
            "all_event" => "all_event",
            _ => null
        };
    }

    private static string? NormalizeRoomOverwriteMode(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => "always",
            "always" => "always",
            "only_empty" => "only_empty",
            _ => null
        };
    }

    private static bool HasAnyRoomPatchField(ParticipantRoomPatchRequest? patch)
    {
        if (patch is null)
        {
            return false;
        }

        return patch.AccommodationDocTabId.HasValue
               || patch.RoomNo is not null
               || patch.RoomType is not null
               || patch.BoardType is not null
               || patch.PersonNo is not null
               || patch.HotelCheckInDate is not null
               || patch.HotelCheckOutDate is not null;
    }

    private static IQueryable<ParticipantEntity> ApplyRoomFilters(
        IQueryable<ParticipantEntity> query,
        ParticipantRoomFiltersRequest? filters,
        Guid eventId,
        Guid organizationId,
        TripflowDbContext db)
    {
        if (filters is null)
        {
            return query;
        }

        var search = filters.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || EF.Functions.ILike(x.FirstName, pattern)
                || EF.Functions.ILike(x.LastName, pattern)
                || EF.Functions.ILike(x.TcNo, pattern)
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern))
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || EF.Functions.ILike(x.CheckInCode, pattern)
                || (x.Details != null && (
                    (x.Details.RoomNo != null && EF.Functions.ILike(x.Details.RoomNo, pattern))
                    || (x.Details.AgencyName != null && EF.Functions.ILike(x.Details.AgencyName, pattern))
                )));
        }

        var status = filters.Status?.Trim().ToLowerInvariant();
        if (status is "arrived" or "not_arrived")
        {
            query = status == "arrived"
                ? query.Where(x => db.CheckIns.Any(c =>
                    c.EventId == eventId && c.OrganizationId == organizationId && c.ParticipantId == x.Id))
                : query.Where(x => !db.CheckIns.Any(c =>
                    c.EventId == eventId && c.OrganizationId == organizationId && c.ParticipantId == x.Id));
        }

        var accommodationFilter = filters.AccommodationFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(accommodationFilter))
        {
            if (string.Equals(accommodationFilter, "unassigned", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Details == null || x.Details.AccommodationDocTabId == null);
            }
            else if (Guid.TryParse(accommodationFilter, out var accommodationDocTabId))
            {
                query = query.Where(x => x.Details != null && x.Details.AccommodationDocTabId == accommodationDocTabId);
            }
        }

        return query;
    }

    private static bool TryApplyRoomPatch(
        ParticipantDetailsEntity details,
        ParticipantRoomPatchRequest patch,
        bool onlyEmpty,
        bool hasAccommodationPatch,
        Guid? normalizedAccommodationDocTabId,
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        out string errorCode,
        out string errorMessage,
        out bool changed)
    {
        errorCode = string.Empty;
        errorMessage = string.Empty;
        changed = false;

        if (hasAccommodationPatch)
        {
            if (TryApplyRoomGuidField(details.AccommodationDocTabId, normalizedAccommodationDocTabId, onlyEmpty, out var nextAccommodationDocTabId))
            {
                details.AccommodationDocTabId = nextAccommodationDocTabId;
                changed = true;
            }
        }

        if (TryApplyRoomTextField(details.RoomNo, patch.RoomNo, onlyEmpty, out var nextRoomNo))
        {
            details.RoomNo = nextRoomNo;
            changed = true;
        }
        if (TryApplyRoomTextField(details.RoomType, patch.RoomType, onlyEmpty, out var nextRoomType))
        {
            details.RoomType = nextRoomType;
            changed = true;
        }
        if (TryApplyRoomTextField(details.BoardType, patch.BoardType, onlyEmpty, out var nextBoardType))
        {
            details.BoardType = nextBoardType;
            changed = true;
        }
        if (TryApplyRoomTextField(details.PersonNo, patch.PersonNo, onlyEmpty, out var nextPersonNo))
        {
            details.PersonNo = nextPersonNo;
            changed = true;
        }

        if (!TryApplyRoomDateField(
                details.HotelCheckInDate,
                patch.HotelCheckInDate,
                onlyEmpty,
                "invalid_hotel_check_in_date",
                out var checkInError,
                out var checkInChanged,
                out var nextHotelCheckInDate))
        {
            errorCode = "invalid_hotel_check_in_date";
            errorMessage = checkInError ?? "Hotel check-in date is invalid.";
            return false;
        }
        if (!TryApplyRoomDateField(
                details.HotelCheckOutDate,
                patch.HotelCheckOutDate,
                onlyEmpty,
                "invalid_hotel_check_out_date",
                out var checkOutError,
                out var checkOutChanged,
                out var nextHotelCheckOutDate))
        {
            errorCode = "invalid_hotel_check_out_date";
            errorMessage = checkOutError ?? "Hotel check-out date is invalid.";
            return false;
        }

        if (!TryValidateStayDates(
                nextHotelCheckInDate,
                nextHotelCheckOutDate,
                eventStartDate,
                eventEndDate,
                out var stayDateError))
        {
            errorCode = "invalid_hotel_date_range";
            errorMessage = stayDateError;
            return false;
        }

        if (checkInChanged || checkOutChanged)
        {
            details.HotelCheckInDate = nextHotelCheckInDate;
            details.HotelCheckOutDate = nextHotelCheckOutDate;
            changed = true;
        }

        return true;
    }

    private static bool TryApplyRoomGuidField(Guid? target, Guid? incoming, bool onlyEmpty, out Guid? updated)
    {
        updated = target;
        if (onlyEmpty && target.HasValue && incoming.HasValue)
        {
            return false;
        }

        if (target == incoming)
        {
            return false;
        }

        updated = incoming;
        return true;
    }

    private static bool TryApplyRoomTextField(string? target, string? incoming, bool onlyEmpty, out string? updated)
    {
        updated = target;
        if (incoming is null)
        {
            return false;
        }

        var normalizedIncoming = incoming.Trim();
        var normalizedTarget = target?.Trim() ?? string.Empty;

        if (onlyEmpty && !string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return false;
        }

        if (string.Equals(normalizedTarget, normalizedIncoming, StringComparison.Ordinal))
        {
            return false;
        }

        updated = string.IsNullOrWhiteSpace(normalizedIncoming) ? null : normalizedIncoming;
        return true;
    }

    private static bool TryApplyRoomDateField(
        DateOnly? target,
        string? incoming,
        bool onlyEmpty,
        string code,
        out string? error,
        out bool changed,
        out DateOnly? updated)
    {
        error = null;
        changed = false;
        updated = target;

        if (incoming is null)
        {
            return true;
        }

        var normalized = incoming.Trim();
        if (onlyEmpty && target.HasValue)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            if (target.HasValue)
            {
                changed = true;
            }
            updated = null;
            return true;
        }

        if (!TryParseDateOnly(normalized, out var parsed))
        {
            error = code == "invalid_hotel_check_in_date"
                ? "Hotel check-in date must be in YYYY-MM-DD format."
                : "Hotel check-out date must be in YYYY-MM-DD format.";
            return false;
        }

        if (target != parsed)
        {
            changed = true;
        }
        updated = parsed;
        return true;
    }

    private static bool TryValidateStayDates(
        DateOnly? checkIn,
        DateOnly? checkOut,
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        out string error)
    {
        if (checkIn.HasValue && (checkIn.Value < eventStartDate || checkIn.Value > eventEndDate))
        {
            error = "Hotel check-in date must be within the event date range.";
            return false;
        }

        if (checkOut.HasValue && (checkOut.Value < eventStartDate || checkOut.Value > eventEndDate))
        {
            error = "Hotel check-out date must be within the event date range.";
            return false;
        }

        if (checkIn.HasValue && checkOut.HasValue && checkOut.Value < checkIn.Value)
        {
            error = "Hotel check-out date must be on or after check-in date.";
            return false;
        }

        error = string.Empty;
        return true;
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

    private static EventDocTabDto MapDocTab(EventDocTabEntity entity)
    {
        return new EventDocTabDto(
            entity.Id,
            entity.EventId,
            entity.Title,
            entity.Type,
            entity.SortOrder,
            entity.IsActive,
            ParseContentJson(entity.ContentJson));
    }

    private static IResult? ValidateDocTabRequest(
        string? titleInput,
        string? typeInput,
        int? sortOrderInput,
        bool? isActiveInput,
        JsonElement? contentInput,
        EventDocTabEntity? existing,
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        out string? title,
        out string? type,
        out int sortOrder,
        out bool isActive,
        out string? contentJson)
    {
        title = titleInput?.Trim();
        type = typeInput?.Trim();

        if (titleInput is null)
        {
            if (existing is null)
            {
                sortOrder = 0;
                isActive = false;
                contentJson = null;
                return EventsHelpers.BadRequest("Title is required.");
            }
            title = existing.Title;
        }
        else if (string.IsNullOrWhiteSpace(title))
        {
            sortOrder = 0;
            isActive = false;
            contentJson = null;
            return EventsHelpers.BadRequest("Title is required.");
        }
        else if (title!.Length > 200)
        {
            sortOrder = 0;
            isActive = false;
            contentJson = null;
            return EventsHelpers.BadRequest("Title must be at most 200 characters.");
        }

        if (typeInput is null)
        {
            if (existing is null)
            {
                sortOrder = 0;
                isActive = false;
                contentJson = null;
                return EventsHelpers.BadRequest("Type is required.");
            }
            type = existing.Type;
        }
        else if (string.IsNullOrWhiteSpace(type))
        {
            sortOrder = 0;
            isActive = false;
            contentJson = null;
            return EventsHelpers.BadRequest("Type is required.");
        }
        else if (type!.Length > 50)
        {
            sortOrder = 0;
            isActive = false;
            contentJson = null;
            return EventsHelpers.BadRequest("Type must be at most 50 characters.");
        }

        if (sortOrderInput.HasValue)
        {
            sortOrder = sortOrderInput.Value;
            if (sortOrder < 1)
            {
                isActive = false;
                contentJson = null;
                return EventsHelpers.BadRequest("Sort order must be >= 1.");
            }
        }
        else
        {
            sortOrder = existing?.SortOrder ?? 1;
        }

        isActive = isActiveInput ?? existing?.IsActive ?? true;

        if (contentInput.HasValue)
        {
            var value = contentInput.Value;
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                contentJson = ResolveDocContent(existing, type!);
            }
            else if (value.ValueKind != JsonValueKind.Object)
            {
                contentJson = null;
                return EventsHelpers.BadRequest("contentJson must be a JSON object.");
            }
            else
            {
                if (IsAccommodationDocType(type))
                {
                    if (!TryNormalizeAccommodationContent(
                        value,
                        eventStartDate,
                        eventEndDate,
                        out contentJson,
                        out var errorCode,
                        out var errorMessage))
                    {
                        return Results.BadRequest(new { code = errorCode, message = errorMessage });
                    }
                }
                else
                {
                    contentJson = JsonSerializer.Serialize(value);
                }
            }
        }
        else
        {
            contentJson = ResolveDocContent(existing, type!);
        }

        return null;
    }

    private static string NormalizeDocType(string? type)
        => type?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsAccommodationDocType(string? type)
        => NormalizeDocType(type) == DocTypeHotel;

    private static bool IsSystemDocType(string? normalizedType)
        => normalizedType is DocTypeInsurance or DocTypeTransfer;

    private static string GetDocTypeCategory(string? normalizedType)
    {
        if (normalizedType is DocTypeInsurance or DocTypeTransfer)
        {
            return DocTypeCategorySystem;
        }

        if (normalizedType == DocTypeHotel)
        {
            return DocTypeCategoryAccommodation;
        }

        return DocTypeCategoryCustom;
    }

    private static string ResolveDocContent(EventDocTabEntity? existing, string resolvedType)
    {
        if (IsAccommodationDocType(resolvedType))
        {
            return existing?.ContentJson ?? BuildAccommodationContentJson(
                hotelName: string.Empty,
                address: string.Empty,
                phone: string.Empty,
                checkInDate: string.Empty,
                checkOutDate: string.Empty,
                checkInNote: string.Empty,
                checkOutNote: string.Empty);
        }

        return existing?.ContentJson ?? "{}";
    }

    private static bool TryNormalizeAccommodationContent(
        JsonElement content,
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        out string normalizedJson,
        out string errorCode,
        out string message)
    {
        errorCode = "invalid_doc_content";
        message = "Accommodation content allows only hotelName, address, phone, checkInDate, checkOutDate, checkInNote, checkOutNote.";
        normalizedJson = string.Empty;

        if (content.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? hotelName = null;
        string? address = null;
        string? phone = null;
        string? checkInDate = null;
        string? checkOutDate = null;
        string? checkInNote = null;
        string? checkOutNote = null;

        foreach (var property in content.EnumerateObject())
        {
            if (!AccommodationContentAllowedKeys.Contains(property.Name))
            {
                return false;
            }

            if (!ReadOptionalTrimmedString(property.Value, out var normalizedValue))
            {
                message = "Accommodation content fields must be string values.";
                return false;
            }

            switch (property.Name)
            {
                case "hotelName":
                    hotelName = normalizedValue;
                    break;
                case "address":
                    address = normalizedValue;
                    break;
                case "phone":
                    phone = normalizedValue;
                    break;
                case "checkInDate":
                    checkInDate = normalizedValue;
                    break;
                case "checkOutDate":
                    checkOutDate = normalizedValue;
                    break;
                case "checkInNote":
                    checkInNote = normalizedValue;
                    break;
                case "checkOutNote":
                    checkOutNote = normalizedValue;
                    break;
            }
        }

        if (!TryParseDateOnly(checkInDate, out var parsedCheckInDate))
        {
            message = "Accommodation check-in date must be in YYYY-MM-DD format.";
            return false;
        }

        if (!TryParseDateOnly(checkOutDate, out var parsedCheckOutDate))
        {
            message = "Accommodation check-out date must be in YYYY-MM-DD format.";
            return false;
        }

        if (parsedCheckInDate.HasValue
            && parsedCheckOutDate.HasValue
            && parsedCheckOutDate.Value < parsedCheckInDate.Value)
        {
            message = "Accommodation check-out date must be on or after check-in date.";
            return false;
        }

        if (parsedCheckInDate.HasValue
            && (parsedCheckInDate.Value < eventStartDate || parsedCheckInDate.Value > eventEndDate))
        {
            message = "Accommodation check-in date must be within the event date range.";
            return false;
        }

        if (parsedCheckOutDate.HasValue
            && (parsedCheckOutDate.Value < eventStartDate || parsedCheckOutDate.Value > eventEndDate))
        {
            message = "Accommodation check-out date must be within the event date range.";
            return false;
        }

        normalizedJson = BuildAccommodationContentJson(
            hotelName: hotelName,
            address: address,
            phone: phone,
            checkInDate: parsedCheckInDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            checkOutDate: parsedCheckOutDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            checkInNote: checkInNote,
            checkOutNote: checkOutNote);

        return true;
    }

    private static string BuildAccommodationContentJson(
        string? hotelName,
        string? address,
        string? phone,
        string? checkInDate,
        string? checkOutDate,
        string? checkInNote,
        string? checkOutNote)
        => JsonSerializer.Serialize(new
        {
            hotelName = hotelName ?? string.Empty,
            address = address ?? string.Empty,
            phone = phone ?? string.Empty,
            checkInDate = checkInDate ?? string.Empty,
            checkOutDate = checkOutDate ?? string.Empty,
            checkInNote = checkInNote ?? string.Empty,
            checkOutNote = checkOutNote ?? string.Empty
        });

    private static bool ReadOptionalTrimmedString(JsonElement value, out string? normalized)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
        {
            normalized = string.Empty;
            return true;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            normalized = value.GetString()?.Trim() ?? string.Empty;
            return true;
        }

        normalized = null;
        return false;
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

    private static FlightSegmentDto[] MapFlightSegments(
        IEnumerable<ParticipantFlightSegmentEntity>? segments,
        ParticipantFlightSegmentDirection direction)
    {
        if (segments is null)
        {
            return [];
        }

        return segments
            .Where(x => x.Direction == direction)
            .OrderBy(x => x.SegmentIndex)
            .Select(x => new FlightSegmentDto(
                x.SegmentIndex,
                x.Airline,
                x.DepartureAirport,
                x.ArrivalAirport,
                x.FlightCode,
                x.DepartureDate?.ToString("yyyy-MM-dd"),
                x.DepartureTime?.ToString("HH:mm"),
                x.ArrivalDate?.ToString("yyyy-MM-dd"),
                x.ArrivalTime?.ToString("HH:mm"),
                x.Pnr,
                x.TicketNo,
                x.BaggagePieces,
                x.BaggageTotalKg,
                x.CabinBaggage))
            .ToArray();
    }

    private sealed record FlightSegmentValidationError(
        string Code,
        string Field,
        string Message);

    private static bool TryParseFlightSegmentDirection(
        string? value,
        out ParticipantFlightSegmentDirection direction)
    {
        if (Enum.TryParse<ParticipantFlightSegmentDirection>(value, true, out direction)
            && direction is ParticipantFlightSegmentDirection.Arrival or ParticipantFlightSegmentDirection.Return)
        {
            return true;
        }

        direction = default;
        return false;
    }

    private static bool TryNormalizeFlightSegments(
        Guid orgId,
        Guid eventId,
        Guid participantId,
        FlightSegmentDto[] source,
        ParticipantFlightSegmentDirection direction,
        out List<ParticipantFlightSegmentEntity> normalized,
        out string error)
    {
        var success = TryNormalizeFlightSegments(
            orgId,
            eventId,
            participantId,
            source,
            direction,
            out normalized,
            out error,
            out _,
            uppercaseAirports: false,
            fieldPrefix: direction.ToString());

        return success;
    }

    private static bool TryNormalizeFlightSegments(
        Guid orgId,
        Guid eventId,
        Guid participantId,
        FlightSegmentDto[] source,
        ParticipantFlightSegmentDirection direction,
        out List<ParticipantFlightSegmentEntity> normalized,
        out string error,
        out FlightSegmentValidationError? validationError,
        bool uppercaseAirports,
        string fieldPrefix)
    {
        normalized = [];
        error = string.Empty;
        validationError = null;

        if (source.Length == 0)
        {
            return true;
        }

        var prepared = new List<(FlightSegmentDto Segment, int Order, string? Airline, string? DepartureAirport,
            string? ArrivalAirport, string? FlightCode, DateOnly? DepartureDate, TimeOnly? DepartureTime,
            DateOnly? ArrivalDate, TimeOnly? ArrivalTime, string? Pnr, string? TicketNo, int? BaggagePieces,
            int? BaggageTotalKg, string? CabinBaggage)>();

        for (var i = 0; i < source.Length; i++)
        {
            var segment = source[i];
            if (segment is null)
            {
                continue;
            }

            if (!TryParseDateOnly(segment.DepartureDate, out var departureDate))
            {
                error = $"{direction} segment {i + 1}: departureDate must be in YYYY-MM-DD format.";
                validationError = new FlightSegmentValidationError(
                    "invalid_flight_segment_field",
                    $"{fieldPrefix}[{i}].departureDate",
                    "Departure date must be in YYYY-MM-DD format.");
                return false;
            }

            if (!TryParseTimeOnly(segment.DepartureTime, out var departureTime))
            {
                error = $"{direction} segment {i + 1}: departureTime must be in HH:mm format.";
                validationError = new FlightSegmentValidationError(
                    "invalid_flight_segment_field",
                    $"{fieldPrefix}[{i}].departureTime",
                    "Departure time must be in HH:mm format.");
                return false;
            }

            if (!TryParseDateOnly(segment.ArrivalDate, out var arrivalDate))
            {
                error = $"{direction} segment {i + 1}: arrivalDate must be in YYYY-MM-DD format.";
                validationError = new FlightSegmentValidationError(
                    "invalid_flight_segment_field",
                    $"{fieldPrefix}[{i}].arrivalDate",
                    "Arrival date must be in YYYY-MM-DD format.");
                return false;
            }

            if (!TryParseTimeOnly(segment.ArrivalTime, out var arrivalTime))
            {
                error = $"{direction} segment {i + 1}: arrivalTime must be in HH:mm format.";
                validationError = new FlightSegmentValidationError(
                    "invalid_flight_segment_field",
                    $"{fieldPrefix}[{i}].arrivalTime",
                    "Arrival time must be in HH:mm format.");
                return false;
            }

            if (segment.BaggagePieces.HasValue && segment.BaggagePieces.Value <= 0)
            {
                error = $"{direction} segment {i + 1}: baggagePieces must be greater than zero.";
                validationError = new FlightSegmentValidationError(
                    "invalid_flight_segment_field",
                    $"{fieldPrefix}[{i}].baggagePieces",
                    "Baggage pieces must be greater than zero.");
                return false;
            }

            if (segment.BaggageTotalKg.HasValue && segment.BaggageTotalKg.Value <= 0)
            {
                error = $"{direction} segment {i + 1}: baggageTotalKg must be greater than zero.";
                validationError = new FlightSegmentValidationError(
                    "invalid_flight_segment_field",
                    $"{fieldPrefix}[{i}].baggageTotalKg",
                    "Baggage total kg must be greater than zero.");
                return false;
            }

            var airline = NormalizeOptionalText(segment.Airline);
            var departureAirport = NormalizeAirportCode(segment.DepartureAirport, uppercaseAirports);
            var arrivalAirport = NormalizeAirportCode(segment.ArrivalAirport, uppercaseAirports);
            var flightCode = NormalizeOptionalText(segment.FlightCode);
            var pnr = NormalizeOptionalText(segment.Pnr);
            var ticketNo = NormalizeOptionalText(segment.TicketNo);
            var cabinBaggage = NormalizeOptionalText(segment.CabinBaggage);

            if (!HasAnyFlightSegmentValue(
                    airline,
                    departureAirport,
                    arrivalAirport,
                    flightCode,
                    departureDate,
                    departureTime,
                    arrivalDate,
                    arrivalTime,
                    pnr,
                    ticketNo,
                    segment.BaggagePieces,
                    segment.BaggageTotalKg,
                    cabinBaggage))
            {
                continue;
            }

            prepared.Add((segment, i, airline, departureAirport, arrivalAirport, flightCode, departureDate, departureTime,
                arrivalDate, arrivalTime, pnr, ticketNo, segment.BaggagePieces, segment.BaggageTotalKg, cabinBaggage));
        }

        var ordered = prepared
            .OrderBy(x => x.Segment.SegmentIndex > 0 ? x.Segment.SegmentIndex : int.MaxValue)
            .ThenBy(x => x.Order)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var row = ordered[i];
            normalized.Add(new ParticipantFlightSegmentEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                EventId = eventId,
                ParticipantId = participantId,
                Direction = direction,
                SegmentIndex = i + 1,
                Airline = row.Airline,
                DepartureAirport = row.DepartureAirport,
                ArrivalAirport = row.ArrivalAirport,
                FlightCode = row.FlightCode,
                DepartureDate = row.DepartureDate,
                DepartureTime = row.DepartureTime,
                ArrivalDate = row.ArrivalDate,
                ArrivalTime = row.ArrivalTime,
                Pnr = row.Pnr,
                TicketNo = row.TicketNo,
                BaggagePieces = row.BaggagePieces,
                BaggageTotalKg = row.BaggageTotalKg,
                CabinBaggage = row.CabinBaggage
            });
        }

        return true;
    }

    private static string? NormalizeAirportCode(string? value, bool uppercase)
    {
        var normalized = NormalizeOptionalText(value);
        if (normalized is null)
        {
            return null;
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static bool HasAnyFlightSegmentValue(
        string? airline,
        string? departureAirport,
        string? arrivalAirport,
        string? flightCode,
        DateOnly? departureDate,
        TimeOnly? departureTime,
        DateOnly? arrivalDate,
        TimeOnly? arrivalTime,
        string? pnr,
        string? ticketNo,
        int? baggagePieces,
        int? baggageTotalKg,
        string? cabinBaggage)
    {
        return !string.IsNullOrWhiteSpace(airline)
               || !string.IsNullOrWhiteSpace(departureAirport)
               || !string.IsNullOrWhiteSpace(arrivalAirport)
               || !string.IsNullOrWhiteSpace(flightCode)
               || departureDate is not null
               || departureTime is not null
               || arrivalDate is not null
               || arrivalTime is not null
               || !string.IsNullOrWhiteSpace(pnr)
               || !string.IsNullOrWhiteSpace(ticketNo)
               || baggagePieces is not null
               || baggageTotalKg is not null
               || !string.IsNullOrWhiteSpace(cabinBaggage);
    }

    private readonly record struct AccommodationDocValidationResult(Guid? Value, IResult? Error);

    private static async Task<AccommodationDocValidationResult> ValidateAccommodationDocReference(
        Guid eventId,
        Guid organizationId,
        Guid? accommodationDocTabId,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!accommodationDocTabId.HasValue || accommodationDocTabId == Guid.Empty)
        {
            return new AccommodationDocValidationResult(null, null);
        }

        var exists = await db.EventDocTabs.AsNoTracking()
            .AnyAsync(tab =>
                tab.Id == accommodationDocTabId.Value
                && tab.EventId == eventId
                && tab.OrganizationId == organizationId
                && IsAccommodationDocType(tab.Type),
                ct);

        if (!exists)
        {
            return new AccommodationDocValidationResult(
                null,
                Results.BadRequest(new
                {
                    code = "invalid_accommodation_doc_tab_id",
                    message = "Accommodation doc tab id must belong to this event and must be of type Hotel."
                }));
        }

        return new AccommodationDocValidationResult(accommodationDocTabId.Value, null);
    }

    private static bool TryApplyDetails(
        ParticipantDetailsEntity details,
        ParticipantDetailsRequest request,
        Guid? accommodationDocTabId,
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        out string error)
    {
        details.RoomNo = request.RoomNo;
        details.RoomType = request.RoomType;
        details.BoardType = request.BoardType;
        details.PersonNo = request.PersonNo;
        details.AgencyName = request.AgencyName;
        details.City = request.City;
        details.FlightCity = request.FlightCity;
        details.AccommodationDocTabId = accommodationDocTabId;
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

        if (!TryValidateStayDates(checkIn, checkOut, eventStartDate, eventEndDate, out error))
        {
            return false;
        }

        details.HotelCheckInDate = checkIn;
        details.HotelCheckOutDate = checkOut;
        var legacyTicketNo = NormalizeOptionalText(request.TicketNo);
        var arrivalTicketNo = NormalizeOptionalText(request.ArrivalTicketNo);
        var returnTicketNo = NormalizeOptionalText(request.ReturnTicketNo);
        if (string.IsNullOrWhiteSpace(arrivalTicketNo) && !string.IsNullOrWhiteSpace(legacyTicketNo))
        {
            arrivalTicketNo = legacyTicketNo;
        }
        details.ArrivalTicketNo = arrivalTicketNo;
        details.ReturnTicketNo = returnTicketNo;
        if (string.IsNullOrWhiteSpace(details.TicketNo) && !string.IsNullOrWhiteSpace(arrivalTicketNo))
        {
            details.TicketNo = arrivalTicketNo;
        }
        else if (string.IsNullOrWhiteSpace(details.TicketNo) && !string.IsNullOrWhiteSpace(legacyTicketNo))
        {
            details.TicketNo = legacyTicketNo;
        }
        details.AttendanceStatus = request.AttendanceStatus;
        details.InsuranceCompanyName = MergeOptionalText(details.InsuranceCompanyName, request.InsuranceCompanyName);
        details.InsurancePolicyNo = MergeOptionalText(details.InsurancePolicyNo, request.InsurancePolicyNo);
        if (!TryMergeDateOnly(details.InsuranceStartDate, request.InsuranceStartDate, out var insuranceStart))
        {
            error = "Insurance start date must be in YYYY-MM-DD format.";
            return false;
        }
        if (!TryMergeDateOnly(details.InsuranceEndDate, request.InsuranceEndDate, out var insuranceEnd))
        {
            error = "Insurance end date must be in YYYY-MM-DD format.";
            return false;
        }
        details.InsuranceStartDate = insuranceStart;
        details.InsuranceEndDate = insuranceEnd;
        details.ArrivalAirline = request.ArrivalAirline;
        details.ArrivalDepartureAirport = request.ArrivalDepartureAirport;
        details.ArrivalArrivalAirport = request.ArrivalArrivalAirport;
        details.ArrivalFlightCode = request.ArrivalFlightCode;
        if (!TryParseDateOnly(request.ArrivalFlightDate, out var arrivalFlightDate))
        {
            error = "Arrival flight date must be in YYYY-MM-DD format.";
            return false;
        }
        details.ArrivalFlightDate = arrivalFlightDate;
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
        details.ArrivalCabinBaggage = request.ArrivalCabinBaggage;
        details.ReturnAirline = request.ReturnAirline;
        details.ReturnDepartureAirport = request.ReturnDepartureAirport;
        details.ReturnArrivalAirport = request.ReturnArrivalAirport;
        details.ReturnFlightCode = request.ReturnFlightCode;
        if (!TryParseDateOnly(request.ReturnFlightDate, out var returnFlightDate))
        {
            error = "Return flight date must be in YYYY-MM-DD format.";
            return false;
        }
        details.ReturnFlightDate = returnFlightDate;
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
        details.ReturnCabinBaggage = request.ReturnCabinBaggage;
        if (!TryParseTimeOnly(request.ArrivalTransferPickupTime, out var arrivalTransferPickupTime))
        {
            error = "Arrival transfer pickup time must be in HH:mm format.";
            return false;
        }
        if (!TryParseTimeOnly(request.ReturnTransferPickupTime, out var returnTransferPickupTime))
        {
            error = "Return transfer pickup time must be in HH:mm format.";
            return false;
        }
        details.ArrivalTransferPickupTime = arrivalTransferPickupTime;
        details.ArrivalTransferPickupPlace = request.ArrivalTransferPickupPlace;
        details.ArrivalTransferDropoffPlace = request.ArrivalTransferDropoffPlace;
        details.ArrivalTransferVehicle = request.ArrivalTransferVehicle;
        details.ArrivalTransferPlate = request.ArrivalTransferPlate;
        details.ArrivalTransferDriverInfo = request.ArrivalTransferDriverInfo;
        details.ArrivalTransferNote = request.ArrivalTransferNote;
        details.ReturnTransferPickupTime = returnTransferPickupTime;
        details.ReturnTransferPickupPlace = request.ReturnTransferPickupPlace;
        details.ReturnTransferDropoffPlace = request.ReturnTransferDropoffPlace;
        details.ReturnTransferVehicle = request.ReturnTransferVehicle;
        details.ReturnTransferPlate = request.ReturnTransferPlate;
        details.ReturnTransferDriverInfo = request.ReturnTransferDriverInfo;
        details.ReturnTransferNote = request.ReturnTransferNote;
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

    private static string? MergeOptionalText(string? existing, string? incoming)
    {
        if (incoming is null)
        {
            return existing;
        }

        var trimmed = incoming.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool TryMergeDateOnly(DateOnly? existing, string? incoming, out DateOnly? date)
    {
        if (incoming is null)
        {
            date = existing;
            return true;
        }

        return TryParseDateOnly(incoming, out date);
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    private static string BuildFullName(string firstName, string lastName)
    {
        var normalizedFirstName = NormalizeName(firstName);
        var normalizedLastName = NormalizeName(lastName);
        return $"{normalizedFirstName} {normalizedLastName}".Trim();
    }

    private static string NormalizeTcNo(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    internal static async Task<IResult> GenerateBadgesPdf(
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

        var participants = await db.Participants.AsNoTracking()
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .ToListAsync(ct);

        var publicBaseUrl = PublicUrlHelper.GetPublicBaseUrl(httpContext);
        var pdfBytes = BadgePdfGenerator.GenerateBadgesPdf(eventEntity, participants, publicBaseUrl);

        var fileName = $"badges_{eventEntity.Name.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }
}
