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
}
