using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Events;

internal static class AccommodationSegmentsReadHelpers
{
    internal sealed record OccupancyWarning(
        string Code,
        string? RoomNo,
        int AssignedCount,
        int DeclaredCount);

    internal sealed record SegmentContext(
        Guid EventId,
        Guid OrganizationId,
        Guid SegmentId,
        Guid DefaultAccommodationDocTabId,
        string DefaultAccommodationTitle,
        DateOnly StartDate,
        DateOnly EndDate);

    internal sealed class SegmentParticipantReadRow
    {
        public Guid ParticipantId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string TcNo { get; init; } = string.Empty;
        public Guid EffectiveAccommodationDocTabId { get; init; }
        public string EffectiveAccommodationTitle { get; init; } = string.Empty;
        public bool UsesOverride { get; init; }
        public string? RoomNo { get; init; }
        public string? RoomType { get; init; }
        public string? BoardType { get; init; }
        public string? PersonNo { get; init; }
    }

    internal sealed record PortalAccommodationSegmentResolved(
        Guid SegmentId,
        string StartDate,
        string EndDate,
        Guid AccommodationDocTabId,
        string AccommodationTitle,
        JsonElement? AccommodationContent,
        string? RoomNo,
        string? RoomType,
        string? BoardType,
        string? PersonNo,
        bool UsesOverride,
        int? NightCount,
        bool IsCurrent,
        bool IsUpcoming,
        string[] Roommates);

    private sealed record RoommateCandidate(
        Guid SegmentId,
        Guid ParticipantId,
        string FullName,
        Guid EffectiveAccommodationDocTabId,
        string RoomNo);

    internal static async Task<SegmentContext?> GetSegmentContextAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        Guid segmentId,
        CancellationToken ct)
    {
        return await db.EventAccommodationSegments.AsNoTracking()
            .Where(x =>
                x.Id == segmentId
                && x.EventId == eventId
                && x.OrganizationId == organizationId)
            .Select(x => new SegmentContext(
                x.EventId,
                x.OrganizationId,
                x.Id,
                x.DefaultAccommodationDocTabId,
                x.DefaultAccommodationDocTab.Title,
                x.StartDate,
                x.EndDate))
            .FirstOrDefaultAsync(ct);
    }

    internal static IQueryable<SegmentParticipantReadRow> BuildSegmentParticipantsQuery(
        TripflowDbContext db,
        IQueryable<ParticipantEntity> participantsQuery,
        SegmentContext context)
    {
        var assignmentsQuery = db.ParticipantAccommodationAssignments.AsNoTracking()
            .Where(x =>
                x.EventId == context.EventId
                && x.OrganizationId == context.OrganizationId
                && x.SegmentId == context.SegmentId);

        return
            from participant in participantsQuery
            join assignment in assignmentsQuery
                on participant.Id equals assignment.ParticipantId into assignmentJoin
            from assignment in assignmentJoin.DefaultIfEmpty()
            join overrideTab in db.EventDocTabs.AsNoTracking()
                on assignment.OverrideAccommodationDocTabId equals overrideTab.Id into overrideJoin
            from overrideTab in overrideJoin.DefaultIfEmpty()
            select new SegmentParticipantReadRow
            {
                ParticipantId = participant.Id,
                FullName = participant.FullName,
                TcNo = participant.TcNo,
                RoomNo = assignment != null ? assignment.RoomNo : null,
                RoomType = assignment != null ? assignment.RoomType : null,
                BoardType = assignment != null ? assignment.BoardType : null,
                PersonNo = assignment != null ? assignment.PersonNo : null,
                UsesOverride = assignment != null && assignment.OverrideAccommodationDocTabId.HasValue,
                EffectiveAccommodationDocTabId = assignment != null && assignment.OverrideAccommodationDocTabId.HasValue
                    ? assignment.OverrideAccommodationDocTabId.Value
                    : context.DefaultAccommodationDocTabId,
                EffectiveAccommodationTitle = overrideTab != null
                    ? overrideTab.Title
                    : context.DefaultAccommodationTitle
            };
    }

    internal static async Task<Dictionary<Guid, string[]>> BuildSegmentRoommatesLookupAsync(
        TripflowDbContext db,
        SegmentContext context,
        IReadOnlyCollection<SegmentParticipantReadRow> rows,
        CancellationToken ct)
    {
        var keys = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.RoomNo))
            .Select(x => new { x.EffectiveAccommodationDocTabId, RoomNo = x.RoomNo!.Trim() })
            .Distinct()
            .ToArray();

        if (keys.Length == 0)
        {
            return [];
        }

        var roomNos = keys.Select(x => x.RoomNo).Distinct().ToArray();
        var accommodationIds = keys.Select(x => x.EffectiveAccommodationDocTabId).Distinct().ToArray();

        var candidates = await (
            from assignment in db.ParticipantAccommodationAssignments.AsNoTracking()
            join participant in db.Participants.AsNoTracking()
                on assignment.ParticipantId equals participant.Id
            where assignment.EventId == context.EventId
                && assignment.OrganizationId == context.OrganizationId
                && assignment.SegmentId == context.SegmentId
                && assignment.RoomNo != null
                && assignment.RoomNo != ""
                && roomNos.Contains(assignment.RoomNo)
            select new RoommateCandidate(
                assignment.SegmentId,
                assignment.ParticipantId,
                participant.FullName,
                assignment.OverrideAccommodationDocTabId ?? context.DefaultAccommodationDocTabId,
                assignment.RoomNo!))
            .ToListAsync(ct);

        var grouped = candidates
            .Where(x => accommodationIds.Contains(x.EffectiveAccommodationDocTabId))
            .GroupBy(x => (x.EffectiveAccommodationDocTabId, x.RoomNo))
            .ToDictionary(
                x => x.Key,
                x => x
                    .GroupBy(item => item.ParticipantId)
                    .Select(item => item.First())
                    .ToArray());

        var result = new Dictionary<Guid, string[]>();
        foreach (var row in rows)
        {
            if (ParsePositiveInteger(row.PersonNo) == 1)
            {
                continue;
            }

            var roomNo = row.RoomNo?.Trim();
            if (string.IsNullOrWhiteSpace(roomNo))
            {
                continue;
            }

            if (!grouped.TryGetValue((row.EffectiveAccommodationDocTabId, roomNo), out var entries))
            {
                continue;
            }

            result[row.ParticipantId] = entries
                .Where(x => x.ParticipantId != row.ParticipantId)
                .Select(x => x.FullName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(10)
                .ToArray();
        }

        return result;
    }

    internal static async Task<Dictionary<Guid, OccupancyWarning[]>> BuildSegmentOccupancyWarningsLookupAsync(
        TripflowDbContext db,
        SegmentContext context,
        IReadOnlyCollection<SegmentParticipantReadRow> rows,
        CancellationToken ct)
    {
        var relevantRows = rows
            .Select(row => new
            {
                Row = row,
                RoomNo = NormalizeText(row.RoomNo),
                DeclaredCount = ParsePositiveInteger(row.PersonNo)
            })
            .Where(x => x.RoomNo is not null && x.DeclaredCount.HasValue)
            .ToArray();

        if (relevantRows.Length == 0)
        {
            return [];
        }

        var roomNos = relevantRows.Select(x => x.RoomNo!).Distinct().ToArray();
        var accommodationIds = relevantRows.Select(x => x.Row.EffectiveAccommodationDocTabId).Distinct().ToArray();

        var candidates = await (
            from assignment in db.ParticipantAccommodationAssignments.AsNoTracking()
            where assignment.EventId == context.EventId
                && assignment.OrganizationId == context.OrganizationId
                && assignment.SegmentId == context.SegmentId
                && assignment.RoomNo != null
                && assignment.RoomNo != ""
                && roomNos.Contains(assignment.RoomNo)
            select new
            {
                assignment.ParticipantId,
                assignment.OverrideAccommodationDocTabId,
                assignment.RoomNo
            })
            .ToListAsync(ct);

        var occupancyByRoom = candidates
            .Select(candidate => new
            {
                candidate.ParticipantId,
                EffectiveAccommodationDocTabId = candidate.OverrideAccommodationDocTabId ?? context.DefaultAccommodationDocTabId,
                RoomNo = candidate.RoomNo!
            })
            .Where(x => accommodationIds.Contains(x.EffectiveAccommodationDocTabId))
            .GroupBy(x => (x.EffectiveAccommodationDocTabId, x.RoomNo))
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(item => item.ParticipantId)
                    .Distinct()
                    .Count());

        var result = new Dictionary<Guid, OccupancyWarning[]>();
        foreach (var row in relevantRows)
        {
            if (!occupancyByRoom.TryGetValue((row.Row.EffectiveAccommodationDocTabId, row.RoomNo!), out var assignedCount))
            {
                continue;
            }

            var declaredCount = row.DeclaredCount!.Value;
            if (assignedCount == declaredCount)
            {
                continue;
            }

            result[row.Row.ParticipantId] =
            [
                new OccupancyWarning(
                    "room_capacity_mismatch",
                    row.RoomNo,
                    assignedCount,
                    declaredCount)
            ];
        }

        return result;
    }

    internal static async Task<PortalAccommodationSegmentResolved[]> BuildPortalAccommodationSegmentsAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        Guid participantId,
        CancellationToken ct)
    {
        var segments = await db.EventAccommodationSegments.AsNoTracking()
            .Where(x => x.EventId == eventId && x.OrganizationId == organizationId)
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new SegmentContext(
                x.EventId,
                x.OrganizationId,
                x.Id,
                x.DefaultAccommodationDocTabId,
                x.DefaultAccommodationDocTab.Title,
                x.StartDate,
                x.EndDate))
            .ToArrayAsync(ct);

        if (segments.Length == 0)
        {
            return [];
        }

        var segmentIds = segments.Select(x => x.SegmentId).ToArray();
        var assignments = await db.ParticipantAccommodationAssignments.AsNoTracking()
            .Where(x =>
                x.EventId == eventId
                && x.OrganizationId == organizationId
                && x.ParticipantId == participantId
                && segmentIds.Contains(x.SegmentId))
            .ToDictionaryAsync(x => x.SegmentId, ct);

        var tabIds = segments.Select(x => x.DefaultAccommodationDocTabId)
            .Concat(assignments.Values.Where(x => x.OverrideAccommodationDocTabId.HasValue).Select(x => x.OverrideAccommodationDocTabId!.Value))
            .Distinct()
            .ToArray();

        var tabs = await db.EventDocTabs.AsNoTracking()
            .Where(x => tabIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var segmentById = segments.ToDictionary(x => x.SegmentId);

        var keys = new List<(Guid SegmentId, Guid AccommodationDocTabId, string RoomNo)>();
        var provisional = new List<(SegmentContext Segment, ParticipantAccommodationAssignmentEntity? Assignment, Guid AccommodationDocTabId)>(segments.Length);

        foreach (var segment in segments)
        {
            assignments.TryGetValue(segment.SegmentId, out var assignment);
            var accommodationDocTabId = assignment?.OverrideAccommodationDocTabId ?? segment.DefaultAccommodationDocTabId;
            provisional.Add((segment, assignment, accommodationDocTabId));

            var roomNo = assignment?.RoomNo?.Trim();
            if (!string.IsNullOrWhiteSpace(roomNo))
            {
                keys.Add((segment.SegmentId, accommodationDocTabId, roomNo));
            }
        }

        var roommatesLookup = await BuildPortalRoommatesLookupAsync(
            db,
            eventId,
            organizationId,
            participantId,
            segmentById,
            keys,
            ct);

        return provisional
            .Select(item =>
            {
                var tab = tabs.GetValueOrDefault(item.AccommodationDocTabId)
                    ?? tabs.GetValueOrDefault(item.Segment.DefaultAccommodationDocTabId);
                var roomNo = NormalizeText(item.Assignment?.RoomNo);
                var roomType = NormalizeText(item.Assignment?.RoomType);
                var boardType = NormalizeText(item.Assignment?.BoardType);
                var personNo = NormalizeText(item.Assignment?.PersonNo);
                var nightCount = item.Segment.EndDate.DayNumber - item.Segment.StartDate.DayNumber;
                var isCurrent = today >= item.Segment.StartDate && today <= item.Segment.EndDate;
                var isUpcoming = item.Segment.StartDate > today;
                var roommates = ParsePositiveInteger(personNo) == 1
                    ? []
                    : !string.IsNullOrWhiteSpace(roomNo)
                    && roommatesLookup.TryGetValue((item.Segment.SegmentId, item.AccommodationDocTabId, roomNo), out var names)
                    ? names
                    : [];

                return new PortalAccommodationSegmentResolved(
                    item.Segment.SegmentId,
                    item.Segment.StartDate.ToString("yyyy-MM-dd"),
                    item.Segment.EndDate.ToString("yyyy-MM-dd"),
                    item.AccommodationDocTabId,
                    tab?.Title ?? item.Segment.DefaultAccommodationTitle,
                    TryParseJson(tab?.ContentJson),
                    roomNo,
                    roomType,
                    boardType,
                    personNo,
                    item.Assignment?.OverrideAccommodationDocTabId.HasValue == true,
                    nightCount >= 0 ? nightCount : null,
                    isCurrent,
                    isUpcoming,
                    roommates);
            })
            .ToArray();
    }

    private static async Task<Dictionary<(Guid SegmentId, Guid AccommodationDocTabId, string RoomNo), string[]>> BuildPortalRoommatesLookupAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        Guid participantId,
        IReadOnlyDictionary<Guid, SegmentContext> segmentById,
        IReadOnlyCollection<(Guid SegmentId, Guid AccommodationDocTabId, string RoomNo)> keys,
        CancellationToken ct)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var segmentIds = keys.Select(x => x.SegmentId).Distinct().ToArray();
        var roomNos = keys.Select(x => x.RoomNo).Distinct().ToArray();
        var accommodationIds = keys.Select(x => x.AccommodationDocTabId).Distinct().ToArray();

        var candidates = await (
            from assignment in db.ParticipantAccommodationAssignments.AsNoTracking()
            join participant in db.Participants.AsNoTracking()
                on assignment.ParticipantId equals participant.Id
            where assignment.EventId == eventId
                && assignment.OrganizationId == organizationId
                && segmentIds.Contains(assignment.SegmentId)
                && assignment.ParticipantId != participantId
                && assignment.RoomNo != null
                && assignment.RoomNo != ""
                && roomNos.Contains(assignment.RoomNo)
            select new
            {
                assignment.SegmentId,
                assignment.ParticipantId,
                participant.FullName,
                assignment.OverrideAccommodationDocTabId,
                assignment.RoomNo
            })
            .ToListAsync(ct);

        return candidates
            .Select(candidate =>
            {
                var defaultAccommodationDocTabId = segmentById[candidate.SegmentId].DefaultAccommodationDocTabId;
                return new RoommateCandidate(
                    candidate.SegmentId,
                    candidate.ParticipantId,
                    candidate.FullName,
                    candidate.OverrideAccommodationDocTabId ?? defaultAccommodationDocTabId,
                    candidate.RoomNo!);
            })
            .Where(x => accommodationIds.Contains(x.EffectiveAccommodationDocTabId))
            .GroupBy(x => (x.SegmentId, x.EffectiveAccommodationDocTabId, x.RoomNo))
            .ToDictionary(
                x => x.Key,
                x => x
                    .GroupBy(item => item.ParticipantId)
                    .Select(item => item.First())
                    .Select(item => item.FullName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct()
                    .Take(10)
                    .ToArray());
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static int? ParsePositiveInteger(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        return int.TryParse(normalized, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    private static JsonElement? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return null;
        }
    }
}
