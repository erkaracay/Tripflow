using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

internal static class PortalMealHandlers
{
    internal static async Task<IResult> GetMeal(
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolvePortalMealContext(activityId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var resolvedContext = context!;
        return Results.Ok(await BuildResponse(resolvedContext, db, ct));
    }

    internal static async Task<IResult> UpsertMealSelections(
        string activityId,
        PortalMealSelectionsUpsertRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var (error, context) = await ResolvePortalMealContext(activityId, httpContext, db, ct);
        if (error is not null)
        {
            return error;
        }

        var resolvedContext = context!;
        var selections = request?.Selections ?? Array.Empty<PortalMealSelectionUpsertItem>();
        var duplicateGroupId = selections
            .GroupBy(x => x.GroupId)
            .Where(g => g.Count() > 1)
            .Select(g => (Guid?)g.Key)
            .FirstOrDefault();
        if (duplicateGroupId.HasValue)
        {
            return MealMenuHelpers.BadRequest("duplicate_group_selection", "Each group can be selected only once.");
        }

        var groups = await db.ActivityMealGroups
            .Include(x => x.Options)
            .Where(x => x.OrganizationId == resolvedContext.OrganizationId && x.EventId == resolvedContext.EventId && x.ActivityId == resolvedContext.Activity.Id)
            .ToListAsync(ct);

        var groupLookup = groups.ToDictionary(x => x.Id);
        var activeGroupIds = groups.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();

        var normalizedSelections = new List<NormalizedPortalMealSelection>();
        foreach (var selection in selections)
        {
            if (!groupLookup.TryGetValue(selection.GroupId, out var group))
            {
                return MealMenuHelpers.BadRequest("invalid_group_for_activity", "groupId does not belong to the activity.");
            }

            if (!group.IsActive)
            {
                return MealMenuHelpers.BadRequest("inactive_group", "Selected group is inactive.");
            }

            var normalizedOtherText = MealMenuHelpers.NormalizeOtherText(selection.OtherText);
            var normalizedNote = group.AllowNote ? MealMenuHelpers.NormalizeNote(selection.Note) : null;

            if (selection.OptionId.HasValue)
            {
                if (normalizedOtherText is not null)
                {
                    return MealMenuHelpers.BadRequest("invalid_option_for_group", "optionId and otherText cannot be used together.");
                }

                var option = group.Options.FirstOrDefault(x => x.Id == selection.OptionId.Value);
                if (option is null)
                {
                    return MealMenuHelpers.BadRequest("invalid_option_for_group", "optionId does not belong to the group.");
                }

                if (!option.IsActive)
                {
                    return MealMenuHelpers.BadRequest("inactive_option", "Selected option is inactive.");
                }

                normalizedSelections.Add(new NormalizedPortalMealSelection(group.Id, option.Id, null, normalizedNote));
                continue;
            }

            if (!group.AllowOther)
            {
                return MealMenuHelpers.BadRequest("other_not_allowed", "Other text is not allowed for this group.");
            }

            if (normalizedOtherText is null)
            {
                return MealMenuHelpers.BadRequest("selection_required", "Either optionId or otherText is required.");
            }

            normalizedSelections.Add(new NormalizedPortalMealSelection(group.Id, null, normalizedOtherText, normalizedNote));
        }

        var now = DateTime.UtcNow;
        var existingSelections = await db.ParticipantMealSelections
            .Where(x => x.OrganizationId == resolvedContext.OrganizationId
                && x.EventId == resolvedContext.EventId
                && x.ActivityId == resolvedContext.Activity.Id
                && x.ParticipantId == resolvedContext.ParticipantId
                && activeGroupIds.Contains(x.GroupId))
            .ToListAsync(ct);
        var existingByGroup = existingSelections.ToDictionary(x => x.GroupId);
        var payloadGroupIds = normalizedSelections.Select(x => x.GroupId).ToHashSet();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var deletions = existingSelections.Where(x => !payloadGroupIds.Contains(x.GroupId)).ToList();
        if (deletions.Count > 0)
        {
            db.ParticipantMealSelections.RemoveRange(deletions);
        }

        foreach (var selection in normalizedSelections)
        {
            if (existingByGroup.TryGetValue(selection.GroupId, out var existing))
            {
                existing.OptionId = selection.OptionId;
                existing.OtherText = selection.OtherText;
                existing.Note = selection.Note;
                existing.UpdatedAt = now;
            }
            else
            {
                db.ParticipantMealSelections.Add(new ParticipantMealSelectionEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = resolvedContext.OrganizationId,
                    EventId = resolvedContext.EventId,
                    ActivityId = resolvedContext.Activity.Id,
                    GroupId = selection.GroupId,
                    ParticipantId = resolvedContext.ParticipantId,
                    OptionId = selection.OptionId,
                    OtherText = selection.OtherText,
                    Note = selection.Note,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Results.Ok(await BuildResponse(resolvedContext, db, ct));
    }

    private static async Task<PortalMealResponse> BuildResponse(
        PortalMealContext context,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var groups = await db.ActivityMealGroups.AsNoTracking()
            .Where(x => x.OrganizationId == context.OrganizationId
                && x.EventId == context.EventId
                && x.ActivityId == context.Activity.Id
                && x.IsActive)
            .Include(x => x.Options.Where(o => o.IsActive))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

        var groupIds = groups.Select(x => x.Id).ToArray();
        var selections = await db.ParticipantMealSelections.AsNoTracking()
            .Where(x => x.OrganizationId == context.OrganizationId
                && x.EventId == context.EventId
                && x.ActivityId == context.Activity.Id
                && x.ParticipantId == context.ParticipantId
                && groupIds.Contains(x.GroupId))
            .ToListAsync(ct);
        var selectionLookup = selections.ToDictionary(x => x.GroupId);

        var responseGroups = groups.Select(group =>
        {
            selectionLookup.TryGetValue(group.Id, out var selection);

            if (selection is not null
                && selection.OptionId.HasValue
                && !group.Options.Any(x => x.Id == selection.OptionId.Value))
            {
                selection = null;
            }

            if (selection is not null
                && !selection.OptionId.HasValue
                && !group.AllowOther)
            {
                selection = null;
            }

            var selectionNote = group.AllowNote ? selection?.Note : null;

            return new PortalMealGroupDto(
                group.Id,
                group.Title,
                group.SortOrder,
                group.AllowOther,
                group.AllowNote,
                group.Options
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Label)
                    .Select(x => new PortalMealOptionDto(x.Id, x.Label, x.SortOrder))
                    .ToArray(),
                selection is null
                    ? null
                    : new PortalMealSelectionDto(selection.GroupId, selection.OptionId, selection.OtherText, selectionNote));
        }).ToArray();

        return new PortalMealResponse(context.Activity.Id, responseGroups);
    }

    private static async Task<(IResult? Error, PortalMealContext? Context)> ResolvePortalMealContext(
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var session = await PortalSessionHelpers.GetValidSessionAsync(httpContext, db, ct);
        if (session is null)
        {
            return (Results.Unauthorized(), null);
        }

        if (!Guid.TryParse(activityId, out var activityGuid))
        {
            return (EventsHelpers.BadRequest("Invalid activity id."), null);
        }

        var activity = await db.EventActivities.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == activityGuid
                && x.EventId == session.EventId
                && x.OrganizationId == session.OrganizationId, ct);
        if (activity is null)
        {
            return (Results.NotFound(new { message = "Activity not found." }), null);
        }

        if (!MealMenuHelpers.IsMealActivity(activity.Type))
        {
            return (MealMenuHelpers.BadRequest("not_meal_activity", "Activity type must be Meal."), null);
        }

        if (session.Participant is null)
        {
            return (Results.Unauthorized(), null);
        }

        return (null, new PortalMealContext(session.OrganizationId, session.EventId, session.ParticipantId, activity));
    }

    private sealed record PortalMealContext(Guid OrganizationId, Guid EventId, Guid ParticipantId, EventActivityEntity Activity);
    private sealed record NormalizedPortalMealSelection(Guid GroupId, Guid? OptionId, string? OtherText, string? Note);
}
