using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class ActivityCheckInHandlers
{
    private const int CheckInCodeMinLength = 6;
    private const int CheckInCodeMaxLength = 10;

    internal static async Task<IResult> PostCheckIn(
        string eventId,
        string activityId,
        ActivityCheckInRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (!Guid.TryParse(activityId, out var actId))
            return EventsHelpers.BadRequest("Invalid activity id.");

        var actorUserId = TryGetActorUserId(httpContext.User);
        var actorRole = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent)) userAgent = null;

        var activity = await db.EventActivities.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == actId && x.EventId == evtId && x.OrganizationId == orgId, ct);
        if (activity is null)
            return Results.NotFound(new { message = "Activity not found." });

        var direction = NormalizeDirection(request.Direction);
        var method = NormalizeMethod(request.Method ?? "Manual");
        var code = NormalizeCheckInCode(request.CheckInCode ?? request.Code);

        if (string.IsNullOrWhiteSpace(code) || code.Length < CheckInCodeMinLength || code.Length > CheckInCodeMaxLength)
        {
            var loggedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.ActivityParticipantLogs.Add(CreateLog(orgId, evtId, actId, null, direction, method, ResultInvalidRequest, actorUserId, actorRole, ipAddress, userAgent, loggedAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.BadRequest(new { result = ResultInvalidRequest, loggedAt, direction = direction, method = method });
        }

        var participant = await db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == evtId && x.OrganizationId == orgId && x.CheckInCode == code, ct);

        if (participant is null)
        {
            var loggedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.ActivityParticipantLogs.Add(CreateLog(orgId, evtId, actId, null, direction, method, ResultNotFound, actorUserId, actorRole, ipAddress, userAgent, loggedAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.NotFound(new { result = ResultNotFound, loggedAt, direction = direction, method = method });
        }

        string result;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (direction == "Entry")
            {
                var lastEntryOrExit = await db.ActivityParticipantLogs.AsNoTracking()
                    .Where(x => x.ActivityId == actId && x.ParticipantId == participant.Id && x.OrganizationId == orgId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new { x.Direction, x.Result })
                    .FirstOrDefaultAsync(ct);
                var isCheckedIn = lastEntryOrExit != null
                    && lastEntryOrExit.Direction == "Entry"
                    && lastEntryOrExit.Result == Success;
                result = isCheckedIn ? AlreadyCheckedIn : Success;
            }
            else
            {
                result = Success;
            }

            var createdAt = DateTime.UtcNow;
            db.ActivityParticipantLogs.Add(CreateLog(orgId, evtId, actId, participant.Id, direction, method, result, actorUserId, actorRole, ipAddress, userAgent, createdAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return Results.Ok(new ActivityCheckInResponse(
            participant.Id,
            participant.FullName,
            result,
            direction,
            method,
            DateTime.UtcNow));
    }

    internal static async Task<IResult> GetParticipantsTable(
        string eventId,
        string activityId,
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
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (!Guid.TryParse(activityId, out var actId))
            return EventsHelpers.BadRequest("Invalid activity id.");

        var activityExists = await db.EventActivities.AsNoTracking()
            .AnyAsync(x => x.Id == actId && x.EventId == evtId && x.OrganizationId == orgId, ct);
        if (!activityExists)
            return Results.NotFound(new { message = "Activity not found." });

        var resolvedPage = Math.Max(1, page ?? 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.EventId == evtId && x.OrganizationId == orgId);

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
                    || (x.Details.AgencyName != null && EF.Functions.ILike(x.Details.AgencyName, pattern))
                )));
        }

        var statusValue = (status ?? "all").Trim().ToLowerInvariant();
        
        // Exclude willNotAttend participants from all filters except "will_not_attend"
        if (statusValue != "will_not_attend")
        {
            var willNotAttendParticipantIds = await db.ParticipantActivityWillNotAttend.AsNoTracking()
                .Where(x => x.ActivityId == actId && x.WillNotAttend)
                .Select(x => x.ParticipantId)
                .ToListAsync(ct);
            if (willNotAttendParticipantIds.Count > 0)
            {
                participantsQuery = participantsQuery.Where(x => !willNotAttendParticipantIds.Contains(x.Id));
            }
        }

        if (statusValue is "checked_in" or "not_checked_in")
        {
            var activityLogsByParticipant = db.ActivityParticipantLogs.AsNoTracking()
                .Where(x => x.ActivityId == actId && x.OrganizationId == orgId && x.ParticipantId != null
                    && (x.Result == Success || x.Result == AlreadyCheckedIn))
                .GroupBy(x => x.ParticipantId!.Value)
                .Select(g => new
                {
                    ParticipantId = g.Key,
                    LastDirection = g.OrderByDescending(x => x.CreatedAt).Select(x => x.Direction).FirstOrDefault(),
                    LastResult = g.OrderByDescending(x => x.CreatedAt).Select(x => x.Result).FirstOrDefault()
                })
                .Where(x => x.LastDirection == "Entry" && (x.LastResult == Success || x.LastResult == AlreadyCheckedIn))
                .Select(x => x.ParticipantId);

            if (statusValue == "checked_in")
                participantsQuery = participantsQuery.Where(x => activityLogsByParticipant.Contains(x.Id));
            else
                participantsQuery = participantsQuery.Where(x => !activityLogsByParticipant.Contains(x.Id));
        }
        else if (statusValue == "will_not_attend")
        {
            var willNotAttendParticipantIds = await db.ParticipantActivityWillNotAttend.AsNoTracking()
                .Where(x => x.ActivityId == actId && x.WillNotAttend)
                .Select(x => x.ParticipantId)
                .ToListAsync(ct);
            participantsQuery = participantsQuery.Where(x => willNotAttendParticipantIds.Contains(x.Id));
        }

        var total = await participantsQuery.CountAsync(ct);

        var sortVal = (sort ?? "fullName").Trim().ToLowerInvariant();
        var desc = (dir ?? "asc").Trim().ToLowerInvariant() == "desc";
        var ordered = sortVal switch
        {
            "roomno" => desc
                ? participantsQuery.OrderByDescending(x => x.Details != null ? x.Details.RoomNo ?? "" : "").ThenBy(x => x.FullName)
                : participantsQuery.OrderBy(x => x.Details != null ? x.Details.RoomNo ?? "" : "").ThenBy(x => x.FullName),
            "agencyname" => desc
                ? participantsQuery.OrderByDescending(x => x.Details != null ? x.Details.AgencyName ?? "" : "").ThenBy(x => x.FullName)
                : participantsQuery.OrderBy(x => x.Details != null ? x.Details.AgencyName ?? "" : "").ThenBy(x => x.FullName),
            _ => desc
                ? participantsQuery.OrderByDescending(x => x.FullName)
                : participantsQuery.OrderBy(x => x.FullName)
        };

        var pageItems = await ordered
            .Include(x => x.Details)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var participantIds = pageItems.Select(x => x.Id).ToList();
        var lastLogs = await db.ActivityParticipantLogs.AsNoTracking()
            .Where(x => x.ActivityId == actId && x.ParticipantId != null && participantIds.Contains(x.ParticipantId!.Value))
            .GroupBy(x => x.ParticipantId!.Value)
            .Select(g => new
            {
                ParticipantId = g.Key,
                Log = g.OrderByDescending(x => x.CreatedAt).Select(x => new { x.Direction, x.Method, x.Result, x.CreatedAt }).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.ParticipantId, x => x.Log, ct);

        var willNotAttendDict = await db.ParticipantActivityWillNotAttend.AsNoTracking()
            .Where(x => x.ActivityId == actId && participantIds.Contains(x.ParticipantId))
            .ToDictionaryAsync(x => x.ParticipantId, x => x.WillNotAttend, ct);

        var items = pageItems.Select(p =>
        {
            lastLogs.TryGetValue(p.Id, out var lastLog);
            var isCheckedIn = lastLog != null && lastLog.Direction == "Entry" && (lastLog.Result == Success || lastLog.Result == AlreadyCheckedIn);
            willNotAttendDict.TryGetValue(p.Id, out var willNotAttend);
            var lastLogDto = lastLog == null ? null : new ActivityLastLogDto(lastLog.Direction, lastLog.Method, lastLog.Result, DateTime.SpecifyKind(lastLog.CreatedAt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            return new ActivityParticipantTableItemDto(
                p.Id,
                p.FullName,
                p.Phone,
                p.Email,
                p.TcNo,
                p.CheckInCode,
                p.Details?.RoomNo,
                p.Details?.AgencyName,
                new ActivityParticipantStateDto(isCheckedIn, willNotAttend, lastLogDto));
        }).ToArray();

        return Results.Ok(new ActivityParticipantTableResponseDto(resolvedPage, resolvedPageSize, total, items));
    }

    private static Guid? TryGetActorUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string NormalizeDirection(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Entry";
        var n = new string((raw.Trim()).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return n == "exit" ? "Exit" : "Entry";
    }

    private static string NormalizeMethod(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Manual";
        var n = new string((raw.Trim()).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return n is "qr" or "qrscan" or "scan" ? "QrScan" : "Manual";
    }

    private static string NormalizeCheckInCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return new string(raw.Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());
    }

    private static ActivityParticipantLogEntity CreateLog(
        Guid orgId, Guid evtId, Guid actId, Guid? participantId,
        string direction, string method, string result,
        Guid? actorUserId, string? actorRole, string? ipAddress, string? userAgent, DateTime createdAt)
    {
        return new ActivityParticipantLogEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EventId = evtId,
            ActivityId = actId,
            ParticipantId = participantId,
            Direction = direction,
            Method = method,
            Result = result,
            ActorUserId = actorUserId,
            ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = createdAt
        };
    }

    internal static async Task<IResult> SetActivityParticipantWillNotAttend(
        string eventId,
        string activityId,
        string participantId,
        ActivityParticipantWillNotAttendRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var evtError))
            return evtError!;
        if (!Guid.TryParse(activityId, out var activityGuid))
            return EventsHelpers.BadRequest("Invalid activity id.");
        if (!Guid.TryParse(participantId, out var participantGuid))
            return EventsHelpers.BadRequest("Invalid participant id.");

        if (request is null || !request.WillNotAttend.HasValue)
            return EventsHelpers.BadRequest("willNotAttend is required.");

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
            return Results.NotFound(new { message = "Event not found." });

        var activityExists = await db.EventActivities.AsNoTracking()
            .AnyAsync(x => x.Id == activityGuid && x.EventId == id && x.OrganizationId == orgId, ct);
        if (!activityExists)
            return Results.NotFound(new { message = "Activity not found." });

        var participant = await db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == participantGuid && x.EventId == id && x.OrganizationId == orgId, ct);
        if (participant is null)
            return Results.NotFound(new { message = "Participant not found." });

        var entity = await db.ParticipantActivityWillNotAttend
            .FirstOrDefaultAsync(x => x.ParticipantId == participantGuid && x.ActivityId == activityGuid, ct);

        if (entity is null)
        {
            entity = new ParticipantActivityWillNotAttendEntity
            {
                Id = Guid.NewGuid(),
                ParticipantId = participantGuid,
                ActivityId = activityGuid,
                WillNotAttend = request.WillNotAttend.Value,
                CreatedAt = DateTime.UtcNow
            };
            db.ParticipantActivityWillNotAttend.Add(entity);
        }
        else
        {
            entity.WillNotAttend = request.WillNotAttend.Value;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var lastLog = await db.ActivityParticipantLogs.AsNoTracking()
            .Where(x => x.OrganizationId == orgId
                        && x.EventId == id
                        && x.ActivityId == activityGuid
                        && x.ParticipantId == participantGuid
                        && (x.Result == Success || x.Result == AlreadyCheckedIn))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ActivityLastLogDto(
                x.Direction,
                x.Method,
                x.Result,
                DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))
            .FirstOrDefaultAsync(ct);

        var isCheckedIn = lastLog != null && lastLog.Direction == "Entry" && (lastLog.Result == Success || lastLog.Result == AlreadyCheckedIn);
        var activityState = new ActivityParticipantStateDto(isCheckedIn, entity.WillNotAttend, lastLog);

        return Results.Ok(new ActivityParticipantWillNotAttendResponse(participantGuid, entity.WillNotAttend, activityState));
    }

    internal static async Task<IResult> ResetAllActivityCheckIns(
        string eventId,
        string activityId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (!Guid.TryParse(activityId, out var actId))
            return EventsHelpers.BadRequest("Invalid activity id.");

        var activityExists = await db.EventActivities.AsNoTracking()
            .AnyAsync(x => x.Id == actId && x.EventId == evtId && x.OrganizationId == orgId, ct);
        if (!activityExists)
            return Results.NotFound(new { message = "Activity not found." });

        var logsToRemove = await db.ActivityParticipantLogs
            .Where(x => x.ActivityId == actId && x.OrganizationId == orgId && x.ParticipantId != null
                && x.Direction == "Entry" && (x.Result == Success || x.Result == AlreadyCheckedIn))
            .ToListAsync(ct);

        var removedCount = logsToRemove.Count;
        if (removedCount > 0)
        {
            db.ActivityParticipantLogs.RemoveRange(logsToRemove);
            await db.SaveChangesAsync(ct);
        }

        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.EventId == evtId && x.OrganizationId == orgId, ct);

        return Results.Ok(new ResetAllActivityCheckInsResponse(removedCount, totalCount));
    }

    private const string Success = "Success";
    private const string AlreadyCheckedIn = "AlreadyCheckedIn";
    private const string ResultNotFound = "NotFound";
    private const string ResultInvalidRequest = "InvalidRequest";
}
