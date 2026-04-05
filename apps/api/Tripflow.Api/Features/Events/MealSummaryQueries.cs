using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;

namespace Tripflow.Api.Features.Events;

internal static class MealSummaryQueries
{
    internal static async Task<MealSummaryResponse> GetSummaryAsync(
        TripflowDbContext db,
        Guid organizationId,
        Guid eventId,
        Guid activityId,
        CancellationToken ct)
    {
        var groups = await db.ActivityMealGroups.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EventId == eventId && x.ActivityId == activityId)
            .Include(x => x.Options)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

        var counts = await db.ParticipantMealSelections.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EventId == eventId && x.ActivityId == activityId)
            .GroupBy(x => new { x.GroupId, x.OptionId })
            .Select(g => new
            {
                g.Key.GroupId,
                g.Key.OptionId,
                Count = g.Count()
            })
            .ToListAsync(ct);

        var noteCounts = await db.ParticipantMealSelections.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId
                && x.EventId == eventId
                && x.ActivityId == activityId
                && x.Note != null)
            .GroupBy(x => x.GroupId)
            .Select(g => new
            {
                GroupId = g.Key,
                Count = g.Count()
            })
            .ToListAsync(ct);

        var countLookup = counts
            .GroupBy(x => x.GroupId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());
        var noteLookup = noteCounts.ToDictionary(x => x.GroupId, x => x.Count);

        var responseGroups = groups.Select(group =>
        {
            countLookup.TryGetValue(group.Id, out var groupCounts);
            var optionCounts = group.Options
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label)
                .Select(option => new MealSummaryCountDto(
                    option.Id,
                    option.Label,
                    groupCounts?.FirstOrDefault(x => x.OptionId == option.Id)?.Count ?? 0))
                .Where(x => x.Count > 0)
                .ToList();

            var otherCount = groupCounts?.FirstOrDefault(x => x.OptionId == null)?.Count ?? 0;
            if (otherCount > 0)
            {
                optionCounts.Add(new MealSummaryCountDto(null, MealMenuHelpers.OtherLabel, otherCount));
            }

            return new MealSummaryGroupDto(
                group.Id,
                group.Title,
                group.AllowOther,
                group.AllowNote,
                optionCounts.ToArray(),
                noteLookup.TryGetValue(group.Id, out var noteCount) ? noteCount : 0);
        }).ToArray();

        return new MealSummaryResponse(activityId, responseGroups);
    }

    internal static async Task<MealShareSummaryResponse> GetShareSummaryAsync(
        TripflowDbContext db,
        Guid organizationId,
        Guid eventId,
        Guid activityId,
        string activityTitle,
        CancellationToken ct)
    {
        var groups = await db.ActivityMealGroups.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EventId == eventId && x.ActivityId == activityId)
            .Include(x => x.Options)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

        var counts = await db.ParticipantMealSelections.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EventId == eventId && x.ActivityId == activityId)
            .GroupBy(x => new { x.GroupId, x.OptionId })
            .Select(g => new
            {
                g.Key.GroupId,
                g.Key.OptionId,
                Count = g.Count()
            })
            .ToListAsync(ct);

        var countLookup = counts
            .GroupBy(x => x.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var responseGroups = groups.Select(group =>
        {
            countLookup.TryGetValue(group.Id, out var groupCounts);
            var optionCounts = group.Options
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label)
                .Select(option => new MealShareSummaryCountDto(
                    option.Label,
                    groupCounts?.FirstOrDefault(x => x.OptionId == option.Id)?.Count ?? 0))
                .Where(x => x.Count > 0)
                .ToList();

            var otherCount = groupCounts?.FirstOrDefault(x => x.OptionId == null)?.Count ?? 0;
            if (otherCount > 0)
            {
                optionCounts.Add(new MealShareSummaryCountDto(MealMenuHelpers.OtherLabel, otherCount));
            }

            return new MealShareSummaryGroupDto(group.Title, optionCounts.ToArray());
        }).ToArray();

        var specialRequests = await (
            from selection in db.ParticipantMealSelections.AsNoTracking()
            join participant in db.Participants.AsNoTracking() on selection.ParticipantId equals participant.Id
            join details in db.ParticipantDetails.AsNoTracking() on participant.Id equals details.ParticipantId into detailsJoin
            from details in detailsJoin.DefaultIfEmpty()
            where selection.OrganizationId == organizationId
                && selection.EventId == eventId
                && selection.ActivityId == activityId
                && (selection.OtherText != null || selection.Note != null)
            orderby participant.FullName
            select new MealShareSummarySpecialRequestDto(
                participant.FullName,
                details != null ? details.RoomNo : null,
                selection.OtherText,
                selection.Note)
        ).ToListAsync(ct);

        return new MealShareSummaryResponse(activityTitle, responseGroups, specialRequests.ToArray());
    }
}
