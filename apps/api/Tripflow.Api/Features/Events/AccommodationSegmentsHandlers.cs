using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class AccommodationSegmentsHandlers
{
    private const string DocTypeHotel = "hotel";

    internal static async Task<IResult> GetSegments(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var eventContext = await ResolveEventContext(eventId, httpContext, db, ct);
        if (eventContext.Error is not null)
        {
            return eventContext.Error;
        }

        var items = await db.EventAccommodationSegments.AsNoTracking()
            .Where(x => x.EventId == eventContext.EventId && x.OrganizationId == eventContext.OrganizationId)
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

    internal static async Task<IResult> CreateSegment(
        string eventId,
        UpsertAccommodationSegmentRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var eventContext = await ResolveEventContext(eventId, httpContext, db, ct);
        if (eventContext.Error is not null)
        {
            return eventContext.Error;
        }

        var defaultAccommodation = await ValidateHotelDocTab(
            eventContext.EventId,
            eventContext.OrganizationId,
            request.DefaultAccommodationDocTabId,
            "defaultAccommodationDocTabId",
            db,
            ct);
        if (defaultAccommodation.Error is not null)
        {
            return defaultAccommodation.Error;
        }

        if (!TryParseSegmentDates(
                request.StartDate,
                request.EndDate,
                eventContext.StartDate,
                eventContext.EndDate,
                out var startDate,
                out var endDate,
                out var dateError))
        {
            return dateError!;
        }

        if (request.SortOrder.HasValue && request.SortOrder.Value < 1)
        {
            return EventsHelpers.BadRequest("invalid_sort_order", "sortOrder", "sortOrder must be at least 1.");
        }

        var overlaps = await HasOverlappingSegment(
            eventContext.EventId,
            eventContext.OrganizationId,
            startDate,
            endDate,
            null,
            db,
            ct);
        if (overlaps)
        {
            return EventsHelpers.BadRequest(
                "segment_overlap",
                "startDate",
                "Segment date range overlaps with an existing accommodation segment.");
        }

        var maxSortOrder = await db.EventAccommodationSegments.AsNoTracking()
            .Where(x => x.EventId == eventContext.EventId && x.OrganizationId == eventContext.OrganizationId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);
        var sortOrder = request.SortOrder ?? ((maxSortOrder ?? 0) + 1);

        var now = DateTime.UtcNow;
        var entity = new EventAccommodationSegmentEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = eventContext.OrganizationId,
            EventId = eventContext.EventId,
            DefaultAccommodationDocTabId = defaultAccommodation.DocTabId!.Value,
            StartDate = startDate,
            EndDate = endDate,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.EventAccommodationSegments.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/events/{eventId}/accommodation-segments/{entity.Id}",
            ToSegmentDto(entity, defaultAccommodation.Title!));
    }

    internal static async Task<IResult> UpdateSegment(
        string eventId,
        Guid segmentId,
        UpsertAccommodationSegmentRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var eventContext = await ResolveEventContext(eventId, httpContext, db, ct);
        if (eventContext.Error is not null)
        {
            return eventContext.Error;
        }

        var segment = await db.EventAccommodationSegments
            .FirstOrDefaultAsync(x =>
                x.Id == segmentId
                && x.EventId == eventContext.EventId
                && x.OrganizationId == eventContext.OrganizationId,
                ct);
        if (segment is null)
        {
            return Results.NotFound(new { message = "Accommodation segment not found." });
        }

        var defaultAccommodation = await ValidateHotelDocTab(
            eventContext.EventId,
            eventContext.OrganizationId,
            request.DefaultAccommodationDocTabId,
            "defaultAccommodationDocTabId",
            db,
            ct);
        if (defaultAccommodation.Error is not null)
        {
            return defaultAccommodation.Error;
        }

        if (!TryParseSegmentDates(
                request.StartDate,
                request.EndDate,
                eventContext.StartDate,
                eventContext.EndDate,
                out var startDate,
                out var endDate,
                out var dateError))
        {
            return dateError!;
        }

        var sortOrder = request.SortOrder ?? segment.SortOrder;
        if (sortOrder < 1)
        {
            return EventsHelpers.BadRequest("invalid_sort_order", "sortOrder", "sortOrder must be at least 1.");
        }

        var overlaps = await HasOverlappingSegment(
            eventContext.EventId,
            eventContext.OrganizationId,
            startDate,
            endDate,
            segment.Id,
            db,
            ct);
        if (overlaps)
        {
            return EventsHelpers.BadRequest(
                "segment_overlap",
                "startDate",
                "Segment date range overlaps with an existing accommodation segment.");
        }

        segment.DefaultAccommodationDocTabId = defaultAccommodation.DocTabId!.Value;
        segment.StartDate = startDate;
        segment.EndDate = endDate;
        segment.SortOrder = sortOrder;
        segment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToSegmentDto(segment, defaultAccommodation.Title!));
    }

    internal static async Task<IResult> DeleteSegment(
        string eventId,
        Guid segmentId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var eventContext = await ResolveEventContext(eventId, httpContext, db, ct);
        if (eventContext.Error is not null)
        {
            return eventContext.Error;
        }

        var segment = await db.EventAccommodationSegments
            .FirstOrDefaultAsync(x =>
                x.Id == segmentId
                && x.EventId == eventContext.EventId
                && x.OrganizationId == eventContext.OrganizationId,
                ct);
        if (segment is null)
        {
            return Results.NotFound(new { message = "Accommodation segment not found." });
        }

        db.EventAccommodationSegments.Remove(segment);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    internal static async Task<IResult> GetSegmentParticipantsTable(
        string eventId,
        Guid segmentId,
        string? query,
        string? accommodationFilter,
        int? page,
        int? pageSize,
        string? sort,
        string? dir,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var segmentContext = await ResolveSegmentContext(eventId, segmentId, httpContext, db, ct);
        if (segmentContext.Error is not null)
        {
            return segmentContext.Error;
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
            .Where(x => x.EventId == segmentContext.EventId && x.OrganizationId == segmentContext.OrganizationId);

        var search = query?.Trim();

        var assignmentsQuery = db.ParticipantAccommodationAssignments.AsNoTracking()
            .Where(x =>
                x.EventId == segmentContext.EventId
                && x.OrganizationId == segmentContext.OrganizationId
                && x.SegmentId == segmentContext.SegmentId);

        var baseQuery =
            from participant in participantsQuery
            join assignment in assignmentsQuery
                on participant.Id equals assignment.ParticipantId into assignmentJoin
            from assignment in assignmentJoin.DefaultIfEmpty()
            join overrideTab in db.EventDocTabs.AsNoTracking()
                on assignment.OverrideAccommodationDocTabId equals overrideTab.Id into overrideJoin
            from overrideTab in overrideJoin.DefaultIfEmpty()
            select new
            {
                participant.Id,
                participant.FullName,
                participant.TcNo,
                RoomNo = assignment != null ? assignment.RoomNo : null,
                RoomType = assignment != null ? assignment.RoomType : null,
                BoardType = assignment != null ? assignment.BoardType : null,
                PersonNo = assignment != null ? assignment.PersonNo : null,
                UsesOverride = assignment != null && assignment.OverrideAccommodationDocTabId.HasValue,
                EffectiveAccommodationDocTabId = assignment != null && assignment.OverrideAccommodationDocTabId.HasValue
                    ? assignment.OverrideAccommodationDocTabId.Value
                    : segmentContext.DefaultAccommodationDocTabId,
                EffectiveAccommodationTitle = overrideTab != null
                    ? overrideTab.Title
                    : segmentContext.DefaultAccommodationTitle
            };

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

        var sortValue = (sort ?? "fullName").Trim().ToLowerInvariant();
        var dirValue = (dir ?? "asc").Trim().ToLowerInvariant();
        var descending = dirValue == "desc";

        var orderedQuery = sortValue switch
        {
            "tcno" => descending
                ? baseQuery.OrderByDescending(x => x.TcNo).ThenBy(x => x.FullName)
                : baseQuery.OrderBy(x => x.TcNo).ThenBy(x => x.FullName),
            "roomno" => descending
                ? baseQuery.OrderByDescending(x => x.RoomNo ?? string.Empty).ThenBy(x => x.FullName)
                : baseQuery.OrderBy(x => x.RoomNo ?? string.Empty).ThenBy(x => x.FullName),
            "accommodation" => descending
                ? baseQuery.OrderByDescending(x => x.EffectiveAccommodationTitle).ThenBy(x => x.FullName)
                : baseQuery.OrderBy(x => x.EffectiveAccommodationTitle).ThenBy(x => x.FullName),
            _ => descending
                ? baseQuery.OrderByDescending(x => x.FullName)
                : baseQuery.OrderBy(x => x.FullName)
        };

        var items = await orderedQuery
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(x => new AccommodationSegmentParticipantTableItemDto(
                x.Id,
                x.FullName,
                x.TcNo,
                x.EffectiveAccommodationDocTabId,
                x.EffectiveAccommodationTitle,
                x.UsesOverride,
                x.RoomNo,
                x.RoomType,
                x.BoardType,
                x.PersonNo))
            .ToArrayAsync(ct);

        return Results.Ok(new AccommodationSegmentParticipantTableResponseDto(
            resolvedPage,
            resolvedPageSize,
            total,
            items));
    }

    internal static async Task<IResult> BulkApplyParticipants(
        string eventId,
        Guid segmentId,
        BulkApplyAccommodationSegmentParticipantsRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EventsHelpers.BadRequest("Request body is required.");
        }

        var segmentContext = await ResolveSegmentContext(eventId, segmentId, httpContext, db, ct);
        if (segmentContext.Error is not null)
        {
            return segmentContext.Error;
        }

        var participantIds = (request.ParticipantIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        if (participantIds.Length == 0)
        {
            return EventsHelpers.BadRequest("participantIds is required.");
        }

        var overwriteMode = NormalizeOverwriteMode(request.OverwriteMode);
        if (overwriteMode is null)
        {
            return Results.BadRequest(new
            {
                code = "invalid_overwrite_mode",
                message = "overwriteMode must be always or only_empty."
            });
        }

        var accommodationMode = NormalizeAccommodationMode(request.AccommodationMode);
        if (accommodationMode is null)
        {
            return Results.BadRequest(new
            {
                code = "invalid_accommodation_mode",
                message = "accommodationMode must be keep, default, or override."
            });
        }

        var roomNoMode = NormalizeFieldMode(request.RoomNoMode);
        var roomTypeMode = NormalizeFieldMode(request.RoomTypeMode);
        var boardTypeMode = NormalizeFieldMode(request.BoardTypeMode);
        var personNoMode = NormalizeFieldMode(request.PersonNoMode);
        if (roomNoMode is null || roomTypeMode is null || boardTypeMode is null || personNoMode is null)
        {
            return Results.BadRequest(new
            {
                code = "invalid_field_mode",
                message = "Field modes must be keep, set, or clear."
            });
        }

        if (accommodationMode == "keep"
            && roomNoMode == "keep"
            && roomTypeMode == "keep"
            && boardTypeMode == "keep"
            && personNoMode == "keep")
        {
            return Results.BadRequest(new
            {
                code = "no_changes_requested",
                message = "At least one field mode or accommodationMode must change data."
            });
        }

        var overrideAccommodation = await ValidateOverrideMode(
            accommodationMode,
            request.OverrideAccommodationDocTabId,
            segmentContext.EventId,
            segmentContext.OrganizationId,
            db,
            ct);
        if (overrideAccommodation.Error is not null)
        {
            return overrideAccommodation.Error;
        }

        var fieldValidations = ValidateTextModes(
            roomNoMode,
            request.RoomNo,
            roomTypeMode,
            request.RoomType,
            boardTypeMode,
            request.BoardType,
            personNoMode,
            request.PersonNo);
        if (fieldValidations.Error is not null)
        {
            return fieldValidations.Error;
        }

        var participantsById = await db.Participants.AsNoTracking()
            .Where(x =>
                x.EventId == segmentContext.EventId
                && x.OrganizationId == segmentContext.OrganizationId
                && participantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var assignmentsByParticipantId = await db.ParticipantAccommodationAssignments
            .Where(x =>
                x.EventId == segmentContext.EventId
                && x.OrganizationId == segmentContext.OrganizationId
                && x.SegmentId == segmentContext.SegmentId
                && participantIds.Contains(x.ParticipantId))
            .ToDictionaryAsync(x => x.ParticipantId, ct);

        var errors = new List<BulkApplyAccommodationSegmentParticipantsErrorDto>();
        var createdCount = 0;
        var updatedCount = 0;
        var deletedCount = 0;
        var unchangedCount = 0;
        var onlyEmpty = overwriteMode == "only_empty";
        var now = DateTime.UtcNow;

        foreach (var participantId in participantIds)
        {
            if (!participantsById.ContainsKey(participantId))
            {
                errors.Add(new BulkApplyAccommodationSegmentParticipantsErrorDto(
                    participantId,
                    "participant_not_found",
                    "Participant could not be resolved for this event."));
                continue;
            }

            assignmentsByParticipantId.TryGetValue(participantId, out var assignment);

            var nextOverrideAccommodationDocTabId = assignment?.OverrideAccommodationDocTabId;
            var nextRoomNo = NormalizeStoredText(assignment?.RoomNo);
            var nextRoomType = NormalizeStoredText(assignment?.RoomType);
            var nextBoardType = NormalizeStoredText(assignment?.BoardType);
            var nextPersonNo = NormalizeStoredText(assignment?.PersonNo);
            var changed = false;

            switch (accommodationMode)
            {
                case "default":
                    if (nextOverrideAccommodationDocTabId.HasValue)
                    {
                        nextOverrideAccommodationDocTabId = null;
                        changed = true;
                    }
                    break;
                case "override":
                    if (nextOverrideAccommodationDocTabId != overrideAccommodation.DocTabId)
                    {
                        nextOverrideAccommodationDocTabId = overrideAccommodation.DocTabId;
                        changed = true;
                    }
                    break;
            }

            changed |= ApplyTextMode(roomNoMode, fieldValidations.RoomNoValue, onlyEmpty, ref nextRoomNo);
            changed |= ApplyTextMode(roomTypeMode, fieldValidations.RoomTypeValue, onlyEmpty, ref nextRoomType);
            changed |= ApplyTextMode(boardTypeMode, fieldValidations.BoardTypeValue, onlyEmpty, ref nextBoardType);
            changed |= ApplyTextMode(personNoMode, fieldValidations.PersonNoValue, onlyEmpty, ref nextPersonNo);

            var emptyRow = IsAssignmentEmpty(
                nextOverrideAccommodationDocTabId,
                nextRoomNo,
                nextRoomType,
                nextBoardType,
                nextPersonNo);

            if (assignment is null)
            {
                if (!changed || emptyRow)
                {
                    unchangedCount++;
                    continue;
                }

                var created = new ParticipantAccommodationAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = segmentContext.OrganizationId,
                    EventId = segmentContext.EventId,
                    ParticipantId = participantId,
                    SegmentId = segmentContext.SegmentId,
                    OverrideAccommodationDocTabId = nextOverrideAccommodationDocTabId,
                    RoomNo = nextRoomNo,
                    RoomType = nextRoomType,
                    BoardType = nextBoardType,
                    PersonNo = nextPersonNo,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.ParticipantAccommodationAssignments.Add(created);
                assignmentsByParticipantId[participantId] = created;
                createdCount++;
                continue;
            }

            if (!changed)
            {
                unchangedCount++;
                continue;
            }

            if (emptyRow)
            {
                db.ParticipantAccommodationAssignments.Remove(assignment);
                assignmentsByParticipantId.Remove(participantId);
                deletedCount++;
                continue;
            }

            assignment.OverrideAccommodationDocTabId = nextOverrideAccommodationDocTabId;
            assignment.RoomNo = nextRoomNo;
            assignment.RoomType = nextRoomType;
            assignment.BoardType = nextBoardType;
            assignment.PersonNo = nextPersonNo;
            assignment.UpdatedAt = now;
            updatedCount++;
        }

        if (createdCount > 0 || updatedCount > 0 || deletedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new BulkApplyAccommodationSegmentParticipantsResponse(
            participantIds.Length,
            createdCount,
            updatedCount,
            deletedCount,
            unchangedCount,
            errors.ToArray()));
    }

    private static AccommodationSegmentDto ToSegmentDto(
        EventAccommodationSegmentEntity entity,
        string defaultAccommodationTitle)
        => new(
            entity.Id,
            entity.DefaultAccommodationDocTabId,
            defaultAccommodationTitle,
            entity.StartDate.ToString("yyyy-MM-dd"),
            entity.EndDate.ToString("yyyy-MM-dd"),
            entity.SortOrder);

    private static async Task<(IResult? Error, Guid EventId, Guid OrganizationId, DateOnly StartDate, DateOnly EndDate)> ResolveEventContext(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var parseError))
        {
            return (parseError, default, default, default, default);
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var organizationId, out var orgError))
        {
            return (orgError, default, default, default, default);
        }

        var eventInfo = await db.Events.AsNoTracking()
            .Where(x => x.Id == id && x.OrganizationId == organizationId)
            .Select(x => new { x.StartDate, x.EndDate })
            .FirstOrDefaultAsync(ct);
        if (eventInfo is null)
        {
            return (Results.NotFound(new { message = "Event not found." }), default, default, default, default);
        }

        return (null, id, organizationId, eventInfo.StartDate, eventInfo.EndDate);
    }

    private static async Task<(IResult? Error, Guid EventId, Guid OrganizationId, Guid SegmentId, Guid DefaultAccommodationDocTabId, string DefaultAccommodationTitle)> ResolveSegmentContext(
        string eventId,
        Guid segmentId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var eventContext = await ResolveEventContext(eventId, httpContext, db, ct);
        if (eventContext.Error is not null)
        {
            return (eventContext.Error, default, default, default, default, string.Empty);
        }

        var segment = await db.EventAccommodationSegments.AsNoTracking()
            .Where(x =>
                x.Id == segmentId
                && x.EventId == eventContext.EventId
                && x.OrganizationId == eventContext.OrganizationId)
            .Select(x => new
            {
                x.Id,
                x.DefaultAccommodationDocTabId,
                DefaultAccommodationTitle = x.DefaultAccommodationDocTab.Title
            })
            .FirstOrDefaultAsync(ct);
        if (segment is null)
        {
            return (Results.NotFound(new { message = "Accommodation segment not found." }), default, default, default, default, string.Empty);
        }

        return (
            null,
            eventContext.EventId,
            eventContext.OrganizationId,
            segment.Id,
            segment.DefaultAccommodationDocTabId,
            segment.DefaultAccommodationTitle);
    }

    private static async Task<(IResult? Error, Guid? DocTabId, string? Title)> ValidateHotelDocTab(
        Guid eventId,
        Guid organizationId,
        Guid? accommodationDocTabId,
        string fieldName,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!accommodationDocTabId.HasValue || accommodationDocTabId == Guid.Empty)
        {
            return (
                EventsHelpers.BadRequest(
                    "invalid_accommodation_doc_tab_id",
                    fieldName,
                    "Accommodation doc tab id must belong to this event and must be of type Hotel."),
                null,
                null);
        }

        var tab = await db.EventDocTabs.AsNoTracking()
            .Where(x =>
                x.Id == accommodationDocTabId.Value
                && x.EventId == eventId
                && x.OrganizationId == organizationId
                && x.Type != null
                && x.Type.ToLower() == DocTypeHotel)
            .Select(x => new { x.Id, x.Title })
            .FirstOrDefaultAsync(ct);
        if (tab is null)
        {
            return (
                EventsHelpers.BadRequest(
                    "invalid_accommodation_doc_tab_id",
                    fieldName,
                    "Accommodation doc tab id must belong to this event and must be of type Hotel."),
                null,
                null);
        }

        return (null, tab.Id, tab.Title);
    }

    private static bool TryParseSegmentDates(
        string? rawStartDate,
        string? rawEndDate,
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        out DateOnly startDate,
        out DateOnly endDate,
        out IResult? error)
    {
        if (!EventsHelpers.TryParseDate(rawStartDate, out startDate))
        {
            endDate = default;
            error = EventsHelpers.BadRequest("invalid_date", "startDate", "startDate must be in YYYY-MM-DD format.");
            return false;
        }

        if (!EventsHelpers.TryParseDate(rawEndDate, out endDate))
        {
            error = EventsHelpers.BadRequest("invalid_date", "endDate", "endDate must be in YYYY-MM-DD format.");
            return false;
        }

        if (endDate < startDate)
        {
            error = EventsHelpers.BadRequest("invalid_date_range", "endDate", "endDate must be on or after startDate.");
            return false;
        }

        if (startDate < eventStartDate || startDate > eventEndDate)
        {
            error = EventsHelpers.BadRequest("date_out_of_range", "startDate", "startDate must be within the event date range.");
            return false;
        }

        if (endDate < eventStartDate || endDate > eventEndDate)
        {
            error = EventsHelpers.BadRequest("date_out_of_range", "endDate", "endDate must be within the event date range.");
            return false;
        }

        error = null;
        return true;
    }

    private static async Task<bool> HasOverlappingSegment(
        Guid eventId,
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeSegmentId,
        TripflowDbContext db,
        CancellationToken ct)
        => await db.EventAccommodationSegments.AsNoTracking()
            .AnyAsync(x =>
                x.EventId == eventId
                && x.OrganizationId == organizationId
                && (!excludeSegmentId.HasValue || x.Id != excludeSegmentId.Value)
                && x.StartDate <= endDate
                && x.EndDate >= startDate,
                ct);

    private static string? NormalizeOverwriteMode(string? raw)
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

    private static string? NormalizeAccommodationMode(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => "keep",
            "keep" => "keep",
            "default" => "default",
            "override" => "override",
            _ => null
        };
    }

    private static string? NormalizeFieldMode(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => "keep",
            "keep" => "keep",
            "set" => "set",
            "clear" => "clear",
            _ => null
        };
    }

    private static async Task<(IResult? Error, Guid? DocTabId)> ValidateOverrideMode(
        string accommodationMode,
        Guid? overrideAccommodationDocTabId,
        Guid eventId,
        Guid organizationId,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (accommodationMode != "override")
        {
            return (null, null);
        }

        var validation = await ValidateHotelDocTab(
            eventId,
            organizationId,
            overrideAccommodationDocTabId,
            "overrideAccommodationDocTabId",
            db,
            ct);
        return (validation.Error, validation.DocTabId);
    }

    private static (IResult? Error, string? RoomNoValue, string? RoomTypeValue, string? BoardTypeValue, string? PersonNoValue) ValidateTextModes(
        string roomNoMode,
        string? roomNo,
        string roomTypeMode,
        string? roomType,
        string boardTypeMode,
        string? boardType,
        string personNoMode,
        string? personNo)
    {
        if (roomNoMode == "set" && NormalizeIncomingText(roomNo) is null)
        {
            return (EventsHelpers.BadRequest("invalid_room_no", "roomNo", "roomNo is required when roomNoMode is set."), null, null, null, null);
        }

        if (roomTypeMode == "set" && NormalizeIncomingText(roomType) is null)
        {
            return (EventsHelpers.BadRequest("invalid_room_type", "roomType", "roomType is required when roomTypeMode is set."), null, null, null, null);
        }

        if (boardTypeMode == "set" && NormalizeIncomingText(boardType) is null)
        {
            return (EventsHelpers.BadRequest("invalid_board_type", "boardType", "boardType is required when boardTypeMode is set."), null, null, null, null);
        }

        if (personNoMode == "set" && NormalizeIncomingText(personNo) is null)
        {
            return (EventsHelpers.BadRequest("invalid_person_no", "personNo", "personNo is required when personNoMode is set."), null, null, null, null);
        }

        return (
            null,
            NormalizeIncomingText(roomNo),
            NormalizeIncomingText(roomType),
            NormalizeIncomingText(boardType),
            NormalizeIncomingText(personNo));
    }

    private static bool ApplyTextMode(
        string mode,
        string? incomingValue,
        bool onlyEmpty,
        ref string? targetValue)
    {
        var normalizedTarget = NormalizeStoredText(targetValue);
        switch (mode)
        {
            case "clear":
                if (normalizedTarget is null)
                {
                    return false;
                }

                targetValue = null;
                return true;
            case "set":
                if (onlyEmpty && normalizedTarget is not null)
                {
                    return false;
                }

                if (string.Equals(normalizedTarget, incomingValue, StringComparison.Ordinal))
                {
                    return false;
                }

                targetValue = incomingValue;
                return true;
            default:
                return false;
        }
    }

    private static bool IsAssignmentEmpty(
        Guid? overrideAccommodationDocTabId,
        string? roomNo,
        string? roomType,
        string? boardType,
        string? personNo)
        => !overrideAccommodationDocTabId.HasValue
           && NormalizeStoredText(roomNo) is null
           && NormalizeStoredText(roomType) is null
           && NormalizeStoredText(boardType) is null
           && NormalizeStoredText(personNo) is null;

    private static string? NormalizeIncomingText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeStoredText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
