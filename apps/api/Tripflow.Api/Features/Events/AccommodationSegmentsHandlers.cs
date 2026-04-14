using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class AccommodationSegmentsHandlers
{
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

        var defaultAccommodation = await AccommodationSegmentAssignmentHelpers.ValidateHotelDocTab(
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

        var defaultAccommodation = await AccommodationSegmentAssignmentHelpers.ValidateHotelDocTab(
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
        string? status,
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

        var statusValue = status?.Trim().ToLowerInvariant();
        if (statusValue is "arrived" or "not_arrived")
        {
            var arrivedParticipantIds = db.CheckIns.AsNoTracking()
                .Where(x => x.EventId == segmentContext.EventId && x.OrganizationId == segmentContext.OrganizationId)
                .Select(x => x.ParticipantId)
                .Distinct();

            participantsQuery = statusValue == "arrived"
                ? participantsQuery.Where(x => arrivedParticipantIds.Contains(x.Id))
                : participantsQuery.Where(x => !arrivedParticipantIds.Contains(x.Id));
        }

        var search = query?.Trim();

        var baseQuery = AccommodationSegmentsReadHelpers.BuildSegmentParticipantsQuery(db, participantsQuery, new AccommodationSegmentsReadHelpers.SegmentContext(
            segmentContext.EventId,
            segmentContext.OrganizationId,
            segmentContext.SegmentId,
            segmentContext.DefaultAccommodationDocTabId,
            segmentContext.DefaultAccommodationTitle,
            default,
            default));

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
            .ToArrayAsync(ct);

        var warningsLookup = await AccommodationSegmentsReadHelpers.BuildSegmentOccupancyWarningsLookupAsync(
            db,
            new AccommodationSegmentsReadHelpers.SegmentContext(
                segmentContext.EventId,
                segmentContext.OrganizationId,
                segmentContext.SegmentId,
                segmentContext.DefaultAccommodationDocTabId,
                segmentContext.DefaultAccommodationTitle,
                default,
                default),
            items,
            ct);

        return Results.Ok(new AccommodationSegmentParticipantTableResponseDto(
            resolvedPage,
            resolvedPageSize,
            total,
            items
                .Select(x => new AccommodationSegmentParticipantTableItemDto(
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
                    warningsLookup.GetValueOrDefault(x.ParticipantId, [])
                        .Select(w => new AccommodationSegmentParticipantWarningDto(
                            w.Code,
                            w.RoomNo,
                            w.AssignedCount,
                            w.DeclaredCount))
                        .ToArray()))
                .ToArray()));
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
        var rowUpdates = (request.RowUpdates ?? Array.Empty<AccommodationSegmentParticipantRowUpdateRequest>())
            .Where(x => x.ParticipantId != Guid.Empty)
            .GroupBy(x => x.ParticipantId)
            .Select(x => x.Last())
            .ToArray();

        var hasSharedMutation = HasSharedMutationRequested(request);
        if (participantIds.Length == 0 && rowUpdates.Length == 0)
        {
            return EventsHelpers.BadRequest("participantIds or rowUpdates is required.");
        }

        if (!hasSharedMutation && rowUpdates.Length == 0)
        {
            return Results.BadRequest(new
            {
                code = "no_changes_requested",
                message = "At least one shared mutation or row update is required."
            });
        }

        if (participantIds.Length == 0 && hasSharedMutation)
        {
            return EventsHelpers.BadRequest("participantIds is required when shared bulk fields are used.");
        }

        var validatedSharedMutation = hasSharedMutation
            ? await ValidateSharedMutation(request, segmentContext.EventId, segmentContext.OrganizationId, db, ct)
            : (Error: (IResult?)null, Mutation: (ValidatedSharedMutation?)null);
        if (validatedSharedMutation.Error is not null)
        {
            return validatedSharedMutation.Error;
        }

        var validatedRowUpdates = await ValidateRowUpdates(
            rowUpdates,
            segmentContext.EventId,
            segmentContext.OrganizationId,
            db,
            ct);
        if (validatedRowUpdates.Error is not null)
        {
            return validatedRowUpdates.Error;
        }

        var targetedParticipantIds = hasSharedMutation
            ? participantIds
                .Concat(validatedRowUpdates.Rows.Select(x => x.ParticipantId))
                .Distinct()
                .ToArray()
            : validatedRowUpdates.Rows
                .Select(x => x.ParticipantId)
                .Distinct()
                .ToArray();

        var participantsById = await db.Participants.AsNoTracking()
            .Where(x =>
                x.EventId == segmentContext.EventId
                && x.OrganizationId == segmentContext.OrganizationId
                && targetedParticipantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var assignmentsByParticipantId = await db.ParticipantAccommodationAssignments
            .Where(x =>
                x.EventId == segmentContext.EventId
                && x.OrganizationId == segmentContext.OrganizationId
                && x.SegmentId == segmentContext.SegmentId
                && targetedParticipantIds.Contains(x.ParticipantId))
            .ToDictionaryAsync(x => x.ParticipantId, ct);

        var errors = new List<BulkApplyAccommodationSegmentParticipantsErrorDto>();
        var createdCount = 0;
        var updatedCount = 0;
        var deletedCount = 0;
        var unchangedCount = 0;
        var now = DateTime.UtcNow;
        var rowUpdateLookup = validatedRowUpdates.Rows.ToDictionary(x => x.ParticipantId);

        foreach (var participantId in targetedParticipantIds)
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

            var result = rowUpdateLookup.TryGetValue(participantId, out var rowUpdate)
                ? ApplyAbsoluteMutation(
                    participantId,
                    assignment,
                    segmentContext.OrganizationId,
                    segmentContext.EventId,
                    segmentContext.SegmentId,
                    rowUpdate,
                    db,
                    now)
                : ApplySharedMutation(
                    participantId,
                    assignment,
                    segmentContext.OrganizationId,
                    segmentContext.EventId,
                    segmentContext.SegmentId,
                    validatedSharedMutation.Mutation!,
                    db,
                    now);

            switch (result.Action)
            {
                case MutationAction.Created:
                    createdCount++;
                    assignmentsByParticipantId[participantId] = result.Entity!;
                    break;
                case MutationAction.Updated:
                    updatedCount++;
                    assignmentsByParticipantId[participantId] = result.Entity!;
                    break;
                case MutationAction.Deleted:
                    deletedCount++;
                    assignmentsByParticipantId.Remove(participantId);
                    break;
                default:
                    unchangedCount++;
                    break;
            }
        }

        if (createdCount > 0 || updatedCount > 0 || deletedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new BulkApplyAccommodationSegmentParticipantsResponse(
            targetedParticipantIds.Length,
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

    private static bool HasSharedMutationRequested(BulkApplyAccommodationSegmentParticipantsRequest request)
        => !string.IsNullOrWhiteSpace(request.AccommodationMode)
           || !string.IsNullOrWhiteSpace(request.RoomNoMode)
           || !string.IsNullOrWhiteSpace(request.RoomTypeMode)
           || !string.IsNullOrWhiteSpace(request.BoardTypeMode)
           || !string.IsNullOrWhiteSpace(request.PersonNoMode);

    private static async Task<(IResult? Error, ValidatedSharedMutation? Mutation)> ValidateSharedMutation(
        BulkApplyAccommodationSegmentParticipantsRequest request,
        Guid eventId,
        Guid organizationId,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var overwriteMode = AccommodationSegmentAssignmentHelpers.NormalizeOverwriteMode(request.OverwriteMode);
        if (overwriteMode is null)
        {
            return (Results.BadRequest(new
            {
                code = "invalid_overwrite_mode",
                message = "overwriteMode must be always or only_empty."
            }), null);
        }

        var accommodationMode = AccommodationSegmentAssignmentHelpers.NormalizeAccommodationMode(request.AccommodationMode);
        if (accommodationMode is null)
        {
            return (Results.BadRequest(new
            {
                code = "invalid_accommodation_mode",
                message = "accommodationMode must be keep, default, or override."
            }), null);
        }

        var roomNoMode = AccommodationSegmentAssignmentHelpers.NormalizeFieldMode(request.RoomNoMode);
        var roomTypeMode = AccommodationSegmentAssignmentHelpers.NormalizeFieldMode(request.RoomTypeMode);
        var boardTypeMode = AccommodationSegmentAssignmentHelpers.NormalizeFieldMode(request.BoardTypeMode);
        var personNoMode = AccommodationSegmentAssignmentHelpers.NormalizeFieldMode(request.PersonNoMode);
        if (roomNoMode is null || roomTypeMode is null || boardTypeMode is null || personNoMode is null)
        {
            return (Results.BadRequest(new
            {
                code = "invalid_field_mode",
                message = "Field modes must be keep, set, or clear."
            }), null);
        }

        if (accommodationMode == "keep"
            && roomNoMode == "keep"
            && roomTypeMode == "keep"
            && boardTypeMode == "keep"
            && personNoMode == "keep")
        {
            return (Results.BadRequest(new
            {
                code = "no_changes_requested",
                message = "At least one field mode or accommodationMode must change data."
            }), null);
        }

        var overrideAccommodation = await AccommodationSegmentAssignmentHelpers.ValidateOverrideMode(
            accommodationMode,
            request.OverrideAccommodationDocTabId,
            eventId,
            organizationId,
            db,
            ct);
        if (overrideAccommodation.Error is not null)
        {
            return (overrideAccommodation.Error, null);
        }

        var fieldValidations = AccommodationSegmentAssignmentHelpers.ValidateTextModes(
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
            return (fieldValidations.Error, null);
        }

        return (null, new ValidatedSharedMutation(
            overwriteMode,
            accommodationMode,
            overrideAccommodation.DocTabId,
            roomNoMode,
            fieldValidations.RoomNoValue,
            roomTypeMode,
            fieldValidations.RoomTypeValue,
            boardTypeMode,
            fieldValidations.BoardTypeValue,
            personNoMode,
            fieldValidations.PersonNoValue));
    }

    private static async Task<(IResult? Error, ValidatedRowUpdate[] Rows)> ValidateRowUpdates(
        AccommodationSegmentParticipantRowUpdateRequest[] rowUpdates,
        Guid eventId,
        Guid organizationId,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var results = new List<ValidatedRowUpdate>(rowUpdates.Length);
        var hotelValidationCache = new Dictionary<Guid, Guid>();

        foreach (var row in rowUpdates)
        {
            var accommodationMode = AccommodationSegmentAssignmentHelpers.NormalizeAccommodationMode(row.AccommodationMode, "default");
            if (accommodationMode is null || accommodationMode == "keep")
            {
                return (Results.BadRequest(new
                {
                    code = "invalid_row_update_accommodation_mode",
                    message = "rowUpdates[].accommodationMode must be default or override."
                }), []);
            }

            Guid? overrideAccommodationDocTabId = null;
            if (accommodationMode == "override")
            {
                if (!row.OverrideAccommodationDocTabId.HasValue || row.OverrideAccommodationDocTabId == Guid.Empty)
                {
                    return (EventsHelpers.BadRequest(
                        "invalid_override_accommodation_doc_tab_id",
                        "rowUpdates.overrideAccommodationDocTabId",
                        "overrideAccommodationDocTabId is required when row update accommodationMode is override."), []);
                }

                if (!hotelValidationCache.TryGetValue(row.OverrideAccommodationDocTabId.Value, out var cachedId))
                {
                    var validation = await AccommodationSegmentAssignmentHelpers.ValidateHotelDocTab(
                        eventId,
                        organizationId,
                        row.OverrideAccommodationDocTabId,
                        "rowUpdates.overrideAccommodationDocTabId",
                        db,
                        ct);
                    if (validation.Error is not null)
                    {
                        return (validation.Error, []);
                    }

                    cachedId = validation.DocTabId!.Value;
                    hotelValidationCache[cachedId] = cachedId;
                }

                overrideAccommodationDocTabId = cachedId;
            }

            results.Add(new ValidatedRowUpdate(
                row.ParticipantId,
                accommodationMode,
                overrideAccommodationDocTabId,
                AccommodationSegmentAssignmentHelpers.NormalizeIncomingText(row.RoomNo),
                AccommodationSegmentAssignmentHelpers.NormalizeIncomingText(row.RoomType),
                AccommodationSegmentAssignmentHelpers.NormalizeIncomingText(row.BoardType),
                AccommodationSegmentAssignmentHelpers.NormalizeIncomingText(row.PersonNo)));
        }

        return (null, results.ToArray());
    }

    private static MutationResult ApplySharedMutation(
        Guid participantId,
        ParticipantAccommodationAssignmentEntity? assignment,
        Guid organizationId,
        Guid eventId,
        Guid segmentId,
        ValidatedSharedMutation mutation,
        TripflowDbContext db,
        DateTime now)
    {
        var nextOverrideAccommodationDocTabId = assignment?.OverrideAccommodationDocTabId;
        var nextRoomNo = AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment?.RoomNo);
        var nextRoomType = AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment?.RoomType);
        var nextBoardType = AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment?.BoardType);
        var nextPersonNo = AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment?.PersonNo);
        var changed = false;

        switch (mutation.AccommodationMode)
        {
            case "default":
                if (nextOverrideAccommodationDocTabId.HasValue)
                {
                    nextOverrideAccommodationDocTabId = null;
                    changed = true;
                }
                break;
            case "override":
                if (nextOverrideAccommodationDocTabId != mutation.OverrideAccommodationDocTabId)
                {
                    nextOverrideAccommodationDocTabId = mutation.OverrideAccommodationDocTabId;
                    changed = true;
                }
                break;
        }

        var onlyEmpty = mutation.OverwriteMode == "only_empty";
        changed |= AccommodationSegmentAssignmentHelpers.ApplyTextMode(mutation.RoomNoMode, mutation.RoomNo, onlyEmpty, ref nextRoomNo);
        changed |= AccommodationSegmentAssignmentHelpers.ApplyTextMode(mutation.RoomTypeMode, mutation.RoomType, onlyEmpty, ref nextRoomType);
        changed |= AccommodationSegmentAssignmentHelpers.ApplyTextMode(mutation.BoardTypeMode, mutation.BoardType, onlyEmpty, ref nextBoardType);
        changed |= AccommodationSegmentAssignmentHelpers.ApplyTextMode(mutation.PersonNoMode, mutation.PersonNo, onlyEmpty, ref nextPersonNo);

        return ApplyResolvedMutation(
            participantId,
            assignment,
            organizationId,
            eventId,
            segmentId,
            nextOverrideAccommodationDocTabId,
            nextRoomNo,
            nextRoomType,
            nextBoardType,
            nextPersonNo,
            changed,
            db,
            now);
    }

    private static MutationResult ApplyAbsoluteMutation(
        Guid participantId,
        ParticipantAccommodationAssignmentEntity? assignment,
        Guid organizationId,
        Guid eventId,
        Guid segmentId,
        ValidatedRowUpdate rowUpdate,
        TripflowDbContext db,
        DateTime now)
    {
        var nextOverrideAccommodationDocTabId = rowUpdate.AccommodationMode == "override"
            ? rowUpdate.OverrideAccommodationDocTabId
            : null;
        var nextRoomNo = rowUpdate.RoomNo;
        var nextRoomType = rowUpdate.RoomType;
        var nextBoardType = rowUpdate.BoardType;
        var nextPersonNo = rowUpdate.PersonNo;

        var changed = assignment is null
            || assignment.OverrideAccommodationDocTabId != nextOverrideAccommodationDocTabId
            || AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment.RoomNo) != nextRoomNo
            || AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment.RoomType) != nextRoomType
            || AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment.BoardType) != nextBoardType
            || AccommodationSegmentAssignmentHelpers.NormalizeStoredText(assignment.PersonNo) != nextPersonNo;

        return ApplyResolvedMutation(
            participantId,
            assignment,
            organizationId,
            eventId,
            segmentId,
            nextOverrideAccommodationDocTabId,
            nextRoomNo,
            nextRoomType,
            nextBoardType,
            nextPersonNo,
            changed,
            db,
            now);
    }

    private static MutationResult ApplyResolvedMutation(
        Guid participantId,
        ParticipantAccommodationAssignmentEntity? assignment,
        Guid organizationId,
        Guid eventId,
        Guid segmentId,
        Guid? nextOverrideAccommodationDocTabId,
        string? nextRoomNo,
        string? nextRoomType,
        string? nextBoardType,
        string? nextPersonNo,
        bool changed,
        TripflowDbContext db,
        DateTime now)
    {
        var emptyRow = AccommodationSegmentAssignmentHelpers.IsAssignmentEmpty(
            nextOverrideAccommodationDocTabId,
            nextRoomNo,
            nextRoomType,
            nextBoardType,
            nextPersonNo);

        if (assignment is null)
        {
            if (!changed || emptyRow)
            {
                return new MutationResult(MutationAction.Unchanged, null);
            }

            var created = new ParticipantAccommodationAssignmentEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventId,
                ParticipantId = participantId,
                SegmentId = segmentId,
                OverrideAccommodationDocTabId = nextOverrideAccommodationDocTabId,
                RoomNo = nextRoomNo,
                RoomType = nextRoomType,
                BoardType = nextBoardType,
                PersonNo = nextPersonNo,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.ParticipantAccommodationAssignments.Add(created);
            return new MutationResult(MutationAction.Created, created);
        }

        if (!changed)
        {
            return new MutationResult(MutationAction.Unchanged, assignment);
        }

        if (emptyRow)
        {
            db.ParticipantAccommodationAssignments.Remove(assignment);
            return new MutationResult(MutationAction.Deleted, null);
        }

        assignment.OverrideAccommodationDocTabId = nextOverrideAccommodationDocTabId;
        assignment.RoomNo = nextRoomNo;
        assignment.RoomType = nextRoomType;
        assignment.BoardType = nextBoardType;
        assignment.PersonNo = nextPersonNo;
        assignment.UpdatedAt = now;
        return new MutationResult(MutationAction.Updated, assignment);
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
                && DoSegmentDateRangesOverlap(x.StartDate, x.EndDate, startDate, endDate),
                ct);

    internal static bool DoSegmentDateRangesOverlap(
        DateOnly leftStartDate,
        DateOnly leftEndDate,
        DateOnly rightStartDate,
        DateOnly rightEndDate)
    {
        if (leftStartDate == rightStartDate && leftEndDate == rightEndDate)
        {
            return true;
        }

        // Segment dates represent check-in/check-out boundaries.
        // Touching ranges like 08-09 and 09-10 are allowed.
        return leftStartDate < rightEndDate && leftEndDate > rightStartDate;
    }

    private sealed record ValidatedSharedMutation(
        string OverwriteMode,
        string AccommodationMode,
        Guid? OverrideAccommodationDocTabId,
        string RoomNoMode,
        string? RoomNo,
        string RoomTypeMode,
        string? RoomType,
        string BoardTypeMode,
        string? BoardType,
        string PersonNoMode,
        string? PersonNo);

    private sealed record ValidatedRowUpdate(
        Guid ParticipantId,
        string AccommodationMode,
        Guid? OverrideAccommodationDocTabId,
        string? RoomNo,
        string? RoomType,
        string? BoardType,
        string? PersonNo);

    private sealed record MutationResult(MutationAction Action, ParticipantAccommodationAssignmentEntity? Entity);

    private enum MutationAction
    {
        Unchanged,
        Created,
        Updated,
        Deleted
    }
}
