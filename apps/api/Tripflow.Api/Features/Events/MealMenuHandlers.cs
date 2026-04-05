using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class MealMenuHandlers
{
    internal static async Task<IResult> GetMealGroups(
        string eventId,
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealActivityContext(eventId, activityId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var resolvedContext = context!;
        var groups = await db.ActivityMealGroups.AsNoTracking()
            .Where(x => x.OrganizationId == resolvedContext.OrganizationId && x.EventId == resolvedContext.EventId && x.ActivityId == resolvedContext.Activity.Id)
            .Include(x => x.Options)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

        return Results.Ok(new MealGroupsResponse(resolvedContext.Activity.Id, groups.Select(MealMenuHelpers.ToGroupDto).ToArray()));
    }

    internal static async Task<IResult> CreateMealGroup(
        string eventId,
        string activityId,
        CreateMealGroupRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
        => await CreateMealGroupForRoute(eventId, activityId, request, "/api/events", httpContext, db, ct);

    internal static async Task<IResult> CreateMealGroupForRoute(
        string eventId,
        string activityId,
        CreateMealGroupRequest request,
        string routeBase,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, group) = await CreateMealGroupCore(eventId, activityId, request, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        return Results.Created(
            $"{routeBase}/{eventId}/meal-groups/{group!.Id}",
            group);
    }

    internal static async Task<IResult> UpdateMealGroup(
        string eventId,
        string groupId,
        UpdateMealGroupRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealGroupContext(eventId, groupId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        if (request is null)
        {
            return MealMenuHelpers.BadRequest("group_title_required", "Request body is required.");
        }

        if (request.Title is not null)
        {
            var title = MealMenuHelpers.NormalizeTitle(request.Title);
            if (title is null)
            {
                return MealMenuHelpers.BadRequest("group_title_required", "Meal group title is required.");
            }

            context!.Group.Title = title;
        }

        if (request.SortOrder.HasValue)
        {
            if (request.SortOrder.Value < 1)
            {
                return MealMenuHelpers.BadRequest("invalid_sort_order", "sortOrder must be at least 1.");
            }

            context!.Group.SortOrder = request.SortOrder.Value;
        }

        if (request.AllowOther.HasValue)
        {
            context!.Group.AllowOther = request.AllowOther.Value;
        }

        if (request.AllowNote.HasValue)
        {
            context!.Group.AllowNote = request.AllowNote.Value;
        }

        if (request.IsActive.HasValue)
        {
            context!.Group.IsActive = request.IsActive.Value;
        }

        await db.SaveChangesAsync(ct);

        var updated = await db.ActivityMealGroups.AsNoTracking()
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == context!.Group.Id, ct);

        return Results.Ok(MealMenuHelpers.ToGroupDto(updated));
    }

    internal static async Task<IResult> DeleteMealGroup(
        string eventId,
        string groupId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealGroupContext(eventId, groupId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var hasSelections = await db.ParticipantMealSelections.AsNoTracking()
            .AnyAsync(x => x.OrganizationId == context!.OrganizationId && x.GroupId == context.Group.Id, ct);
        if (hasSelections)
        {
            return MealMenuHelpers.Conflict("group_in_use", "Meal group cannot be deleted because participant selections exist.");
        }

        db.ActivityMealGroups.Remove(context!.Group);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    internal static async Task<IResult> CreateMealOption(
        string eventId,
        string groupId,
        CreateMealOptionRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
        => await CreateMealOptionForRoute(eventId, groupId, request, "/api/events", httpContext, db, ct);

    internal static async Task<IResult> CreateMealOptionForRoute(
        string eventId,
        string groupId,
        CreateMealOptionRequest request,
        string routeBase,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, option) = await CreateMealOptionCore(eventId, groupId, request, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        return Results.Created(
            $"{routeBase}/{eventId}/meal-options/{option!.Id}",
            option);
    }

    internal static async Task<IResult> UpdateMealOption(
        string eventId,
        string optionId,
        UpdateMealOptionRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealOptionContext(eventId, optionId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        if (request is null)
        {
            return MealMenuHelpers.BadRequest("option_label_required", "Request body is required.");
        }

        if (request.Label is not null)
        {
            var label = MealMenuHelpers.NormalizeLabel(request.Label);
            if (label is null)
            {
                return MealMenuHelpers.BadRequest("option_label_required", "Meal option label is required.");
            }

            context!.Option.Label = label;
        }

        if (request.SortOrder.HasValue)
        {
            if (request.SortOrder.Value < 1)
            {
                return MealMenuHelpers.BadRequest("invalid_sort_order", "sortOrder must be at least 1.");
            }

            context!.Option.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            context!.Option.IsActive = request.IsActive.Value;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(MealMenuHelpers.ToOptionDto(context!.Option));
    }

    internal static async Task<IResult> DeleteMealOption(
        string eventId,
        string optionId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealOptionContext(eventId, optionId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var hasSelections = await db.ParticipantMealSelections.AsNoTracking()
            .AnyAsync(x => x.OrganizationId == context!.OrganizationId && x.OptionId == context.Option.Id, ct);
        if (hasSelections)
        {
            return MealMenuHelpers.Conflict("option_in_use", "Meal option cannot be deleted because participant selections exist.");
        }

        db.ActivityMealOptions.Remove(context!.Option);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    internal static async Task<IResult> GetMealSummary(
        string eventId,
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealActivityContext(eventId, activityId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var resolvedContext = context!;
        var summary = await MealSummaryQueries.GetSummaryAsync(
            db,
            resolvedContext.OrganizationId,
            resolvedContext.EventId,
            resolvedContext.Activity.Id,
            ct);

        return Results.Ok(summary);
    }

    internal static async Task<IResult> GetMealShareSummary(
        string eventId,
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealActivityContext(eventId, activityId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var resolvedContext = context!;
        var result = await MealSummaryQueries.GetShareSummaryAsync(
            db,
            resolvedContext.OrganizationId,
            resolvedContext.EventId,
            resolvedContext.Activity.Id,
            resolvedContext.Activity.Title,
            ct);

        return Results.Ok(result);
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
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealActivityContext(eventId, activityId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var resolvedContext = context!;
        if (!Guid.TryParse(groupId, out var groupGuid))
        {
            return MealMenuHelpers.BadRequest("invalid_group_for_activity", "groupId is required and must be a valid UUID.");
        }

        var groupEntity = await db.ActivityMealGroups.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == groupGuid
                && x.OrganizationId == resolvedContext.OrganizationId
                && x.EventId == resolvedContext.EventId
                && x.ActivityId == resolvedContext.Activity.Id, ct);
        if (groupEntity is null)
        {
            return MealMenuHelpers.BadRequest("invalid_group_for_activity", "groupId does not belong to the activity.");
        }

        if (!MealMenuHelpers.TryParseOptionFilter(optionId, onlyOther == true, out var parsedOptionId, out var filterOther, out var filterError))
        {
            return filterError!;
        }

        string? optionLabel = null;
        if (parsedOptionId.HasValue)
        {
            var option = await db.ActivityMealOptions.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == parsedOptionId.Value
                    && x.GroupId == groupEntity.Id
                    && x.OrganizationId == resolvedContext.OrganizationId, ct);
            if (option is null)
            {
                return MealMenuHelpers.BadRequest("invalid_option_for_group", "optionId does not belong to the group.");
            }

            optionLabel = option.Label;
        }

        var resolvedPage = Math.Max(1, page ?? 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);

        var baseQuery =
            from selection in db.ParticipantMealSelections.AsNoTracking()
            join participant in db.Participants.AsNoTracking() on selection.ParticipantId equals participant.Id
            join details in db.ParticipantDetails.AsNoTracking() on participant.Id equals details.ParticipantId into detailsJoin
            from details in detailsJoin.DefaultIfEmpty()
            join option in db.ActivityMealOptions.AsNoTracking() on selection.OptionId equals option.Id into optionJoin
            from option in optionJoin.DefaultIfEmpty()
            where selection.OrganizationId == resolvedContext.OrganizationId
                  && selection.EventId == resolvedContext.EventId
                  && selection.ActivityId == resolvedContext.Activity.Id
                  && selection.GroupId == groupEntity.Id
            select new
            {
                Selection = selection,
                Participant = participant,
                Details = details,
                Option = option
            };

        if (filterOther)
        {
            baseQuery = baseQuery.Where(x => x.Selection.OptionId == null);
        }
        else if (parsedOptionId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Selection.OptionId == parsedOptionId.Value);
        }

        if (onlyNotes == true)
        {
            baseQuery = baseQuery.Where(x => x.Selection.Note != null);
        }

        var search = q?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            baseQuery = baseQuery.Where(x =>
                EF.Functions.ILike(x.Participant.FullName, pattern)
                || EF.Functions.ILike(x.Participant.FirstName, pattern)
                || EF.Functions.ILike(x.Participant.LastName, pattern)
                || EF.Functions.ILike(x.Participant.TcNo, pattern)
                || EF.Functions.ILike(x.Participant.Phone, pattern)
                || (x.Details != null && x.Details.RoomNo != null && EF.Functions.ILike(x.Details.RoomNo, pattern)));
        }

        var total = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .OrderBy(x => x.Participant.FullName)
            .ThenBy(x => x.Participant.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var items = rows.Select(row => new MealChoiceListItemDto(
            new MealChoiceParticipantDto(
                row.Participant.Id,
                row.Participant.FullName,
                row.Details?.RoomNo,
                row.Participant.Phone),
            row.Selection.OptionId,
            row.Option?.Label ?? optionLabel,
            row.Selection.OtherText,
            row.Selection.Note,
            MealMenuHelpers.FormatUtc(row.Selection.UpdatedAt)))
            .ToArray();

        return Results.Ok(new MealChoiceListResponse(resolvedPage, resolvedPageSize, total, items));
    }

    private static async Task<(IResult? Error, MealActivityContext? Context)> ResolveMealActivityContext(
        string eventId,
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return (orgError, null);
        }

        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var eventError))
        {
            return (eventError, null);
        }

        if (!Guid.TryParse(activityId, out var activityGuid))
        {
            return (EventsHelpers.BadRequest("Invalid activity id."), null);
        }

        var activity = await db.EventActivities
            .FirstOrDefaultAsync(x => x.Id == activityGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (activity is null)
        {
            return (Results.NotFound(new { message = "Activity not found." }), null);
        }

        if (!MealMenuHelpers.IsMealActivity(activity.Type))
        {
            return (MealMenuHelpers.BadRequest("not_meal_activity", "Activity type must be Meal."), null);
        }

        return (null, new MealActivityContext(orgId, eventGuid, activity));
    }

    private static async Task<(IResult? Error, MealGroupContext? Context)> ResolveMealGroupContext(
        string eventId,
        string groupId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return (orgError, null);
        }

        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var eventError))
        {
            return (eventError, null);
        }

        if (!Guid.TryParse(groupId, out var groupGuid))
        {
            return (EventsHelpers.BadRequest("Invalid meal group id."), null);
        }

        var group = await db.ActivityMealGroups
            .Include(x => x.Activity)
            .FirstOrDefaultAsync(x => x.Id == groupGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);
        if (group is null)
        {
            return (Results.NotFound(new { code = "group_not_found", message = "Meal group not found." }), null);
        }

        if (!MealMenuHelpers.IsMealActivity(group.Activity.Type))
        {
            return (MealMenuHelpers.BadRequest("not_meal_activity", "Activity type must be Meal."), null);
        }

        return (null, new MealGroupContext(orgId, eventGuid, group, group.Activity));
    }

    private static async Task<(IResult? Error, MealOptionContext? Context)> ResolveMealOptionContext(
        string eventId,
        string optionId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return (orgError, null);
        }

        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var eventError))
        {
            return (eventError, null);
        }

        if (!Guid.TryParse(optionId, out var optionGuid))
        {
            return (EventsHelpers.BadRequest("Invalid meal option id."), null);
        }

        var option = await db.ActivityMealOptions
            .Include(x => x.Group)
            .ThenInclude(x => x.Activity)
            .FirstOrDefaultAsync(x => x.Id == optionGuid && x.OrganizationId == orgId && x.Group.EventId == eventGuid, ct);
        if (option is null)
        {
            return (Results.NotFound(new { code = "option_not_found", message = "Meal option not found." }), null);
        }

        if (!MealMenuHelpers.IsMealActivity(option.Group.Activity.Type))
        {
            return (MealMenuHelpers.BadRequest("not_meal_activity", "Activity type must be Meal."), null);
        }

        return (null, new MealOptionContext(orgId, eventGuid, option, option.Group, option.Group.Activity));
    }

    private static async Task<(IResult? Error, MealGroupDto? Group)> CreateMealGroupCore(
        string eventId,
        string activityId,
        CreateMealGroupRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealActivityContext(eventId, activityId, httpContext, db, ct);
        if (error is not null)
        {
            return (error, null);
        }

        var resolvedContext = context!;
        if (request is null)
        {
            return (MealMenuHelpers.BadRequest("group_title_required", "Request body is required."), null);
        }

        var title = MealMenuHelpers.NormalizeTitle(request.Title);
        if (title is null)
        {
            return (MealMenuHelpers.BadRequest("group_title_required", "Meal group title is required."), null);
        }

        if (request.SortOrder.HasValue && request.SortOrder.Value < 1)
        {
            return (MealMenuHelpers.BadRequest("invalid_sort_order", "sortOrder must be at least 1."), null);
        }

        var sortOrder = request.SortOrder ?? ((await db.ActivityMealGroups.AsNoTracking()
            .Where(x => x.OrganizationId == resolvedContext.OrganizationId && x.ActivityId == resolvedContext.Activity.Id)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct) ?? 0) + 1);

        var entity = new ActivityMealGroupEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = resolvedContext.OrganizationId,
            EventId = resolvedContext.EventId,
            ActivityId = resolvedContext.Activity.Id,
            Title = title,
            SortOrder = sortOrder,
            AllowOther = request.AllowOther ?? true,
            AllowNote = request.AllowNote ?? true,
            IsActive = request.IsActive ?? true
        };

        db.ActivityMealGroups.Add(entity);
        await db.SaveChangesAsync(ct);

        return (null, new MealGroupDto(
            entity.Id,
            entity.ActivityId,
            entity.Title,
            entity.SortOrder,
            entity.AllowOther,
            entity.AllowNote,
            entity.IsActive,
            Array.Empty<MealOptionDto>()));
    }

    private static async Task<(IResult? Error, MealOptionDto? Option)> CreateMealOptionCore(
        string eventId,
        string groupId,
        CreateMealOptionRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolveMealGroupContext(eventId, groupId, httpContext, db, ct);
        if (error is not null)
        {
            return (error, null);
        }

        if (request is null)
        {
            return (MealMenuHelpers.BadRequest("option_label_required", "Request body is required."), null);
        }

        var label = MealMenuHelpers.NormalizeLabel(request.Label);
        if (label is null)
        {
            return (MealMenuHelpers.BadRequest("option_label_required", "Meal option label is required."), null);
        }

        if (request.SortOrder.HasValue && request.SortOrder.Value < 1)
        {
            return (MealMenuHelpers.BadRequest("invalid_sort_order", "sortOrder must be at least 1."), null);
        }

        var sortOrder = request.SortOrder ?? ((await db.ActivityMealOptions.AsNoTracking()
            .Where(x => x.OrganizationId == context!.OrganizationId && x.GroupId == context.Group.Id)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct) ?? 0) + 1);

        var entity = new ActivityMealOptionEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = context!.OrganizationId,
            GroupId = context.Group.Id,
            Label = label,
            SortOrder = sortOrder,
            IsActive = request.IsActive ?? true
        };

        db.ActivityMealOptions.Add(entity);
        await db.SaveChangesAsync(ct);

        return (null, MealMenuHelpers.ToOptionDto(entity));
    }

    private sealed record MealActivityContext(Guid OrganizationId, Guid EventId, EventActivityEntity Activity);
    private sealed record MealGroupContext(Guid OrganizationId, Guid EventId, ActivityMealGroupEntity Group, EventActivityEntity Activity);
    private sealed record MealOptionContext(
        Guid OrganizationId,
        Guid EventId,
        ActivityMealOptionEntity Option,
        ActivityMealGroupEntity Group,
        EventActivityEntity Activity);
}
