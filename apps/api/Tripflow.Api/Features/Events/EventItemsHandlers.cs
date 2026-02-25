using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class EventItemsHandlers
{
    private const int CheckInCodeMinLength = 6;
    private const int CheckInCodeMaxLength = 10;
    private const string ResultSuccess = "Success";
    private const string ResultNotFound = "NotFound";
    private const string ResultInvalidRequest = "InvalidRequest";

    internal static async Task<IResult> GetItems(
        string eventId,
        bool? includeInactive,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;

        var eventExists = await db.Events.AsNoTracking().AnyAsync(x => x.Id == evtId && x.OrganizationId == orgId, ct);
        if (!eventExists)
            return Results.NotFound(new { message = "Event not found." });

        var query = db.EventItems.AsNoTracking()
            .Where(x => x.EventId == evtId && x.OrganizationId == orgId);
        if (includeInactive != true)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new EventItemDto(x.Id, x.Type, x.Title, x.Name, x.IsActive, x.SortOrder))
            .ToArrayAsync(ct);

        return Results.Ok(items);
    }

    internal static async Task<IResult> CreateItem(
        string eventId,
        CreateEventItemRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return EventsHelpers.BadRequest("Name is required.");

        var eventExists = await db.Events.AsNoTracking().AnyAsync(x => x.Id == evtId && x.OrganizationId == orgId, ct);
        if (!eventExists)
            return Results.NotFound(new { message = "Event not found." });

        var type = string.IsNullOrWhiteSpace(request.Type) ? "Equipment" : request.Type.Trim();
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Equipment" : request.Title.Trim();
        var name = request.Name.Trim();
        if (name.Length > 100) name = name[..100];
        if (type.Length > 32) type = type[..32];
        if (title.Length > 100) title = title[..100];

        var maxOrder = await db.EventItems
            .Where(x => x.EventId == evtId && x.OrganizationId == orgId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct) ?? 0;
        var sortOrder = request.SortOrder ?? (maxOrder + 1);

        var entity = new EventItemEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EventId = evtId,
            Type = type,
            Title = title,
            Name = name,
            IsActive = true,
            SortOrder = sortOrder
        };
        db.EventItems.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/events/{eventId}/items/{entity.Id}", new EventItemDto(entity.Id, entity.Type, entity.Title, entity.Name, entity.IsActive, entity.SortOrder));
    }

    internal static async Task<IResult> UpdateItem(
        string eventId,
        string itemId,
        UpdateEventItemRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (!Guid.TryParse(itemId, out var itId))
            return EventsHelpers.BadRequest("Invalid item id.");
        if (request is null)
            return EventsHelpers.BadRequest("Request body is required.");

        var entity = await db.EventItems.FirstOrDefaultAsync(x => x.Id == itId && x.EventId == evtId && x.OrganizationId == orgId, ct);
        if (entity is null)
            return Results.NotFound(new { message = "Item not found." });

        if (request.Type is { } t && !string.IsNullOrWhiteSpace(t)) entity.Type = t.Trim().Length > 32 ? t.Trim()[..32] : t.Trim();
        if (request.Title is { } tl && !string.IsNullOrWhiteSpace(tl)) entity.Title = tl.Trim().Length > 100 ? tl.Trim()[..100] : tl.Trim();
        if (request.Name is { } n)
        {
            var name = n.Trim();
            if (string.IsNullOrEmpty(name)) return EventsHelpers.BadRequest("Name cannot be empty.");
            entity.Name = name.Length > 100 ? name[..100] : name;
        }
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.SortOrder.HasValue) entity.SortOrder = request.SortOrder.Value;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new EventItemDto(entity.Id, entity.Type, entity.Title, entity.Name, entity.IsActive, entity.SortOrder));
    }

    internal static async Task<IResult> DeleteItem(
        string eventId,
        string itemId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (!Guid.TryParse(itemId, out var itId))
            return EventsHelpers.BadRequest("Invalid item id.");

        var entity = await db.EventItems.FirstOrDefaultAsync(x => x.Id == itId && x.EventId == evtId && x.OrganizationId == orgId, ct);
        if (entity is null)
            return Results.NotFound(new { message = "Item not found." });

        db.EventItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    internal static async Task<IResult> PostAction(
        string eventId,
        string itemId,
        ItemActionRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
            return orgError!;
        if (!EventsHelpers.TryParseEventId(eventId, out var evtId, out var evtError))
            return evtError!;
        if (!Guid.TryParse(itemId, out var itId))
            return EventsHelpers.BadRequest("Invalid item id.");

        var item = await db.EventItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itId && x.EventId == evtId && x.OrganizationId == orgId && x.IsActive, ct);
        if (item is null)
            return Results.NotFound(new { message = "Item not found." });

        var actorUserId = TryGetActorUserId(httpContext.User);
        var actorRole = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent)) userAgent = null;

        var action = NormalizeAction(request.Action);
        var method = NormalizeMethod(request.Method);
        var code = NormalizeCheckInCode(request.CheckInCode ?? request.Code);

        if (string.IsNullOrWhiteSpace(code) || code.Length < CheckInCodeMinLength || code.Length > CheckInCodeMaxLength)
        {
            var loggedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.ParticipantItemLogs.Add(CreateLog(orgId, evtId, itId, null, action, method, ResultInvalidRequest, actorUserId, actorRole, ipAddress, userAgent, loggedAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.BadRequest(new { result = ResultInvalidRequest, loggedAt, action, method });
        }

        var participant = await db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == evtId && x.OrganizationId == orgId && x.CheckInCode == code, ct);

        if (participant is null)
        {
            var loggedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.ParticipantItemLogs.Add(CreateLog(orgId, evtId, itId, null, action, method, ResultNotFound, actorUserId, actorRole, ipAddress, userAgent, loggedAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.NotFound(new { result = ResultNotFound, loggedAt, action, method });
        }

        var createdAt = DateTime.UtcNow;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            db.ParticipantItemLogs.Add(CreateLog(orgId, evtId, itId, participant.Id, action, method, ResultSuccess, actorUserId, actorRole, ipAddress, userAgent, createdAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return Results.Ok(new ItemActionResponse(participant.Id, participant.FullName, ResultSuccess, action, method, createdAt));
    }

    internal static async Task<IResult> GetParticipantsTable(
        string eventId,
        string itemId,
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
        if (!Guid.TryParse(itemId, out var itId))
            return EventsHelpers.BadRequest("Invalid item id.");

        var item = await db.EventItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itId && x.EventId == evtId && x.OrganizationId == orgId, ct);
        if (item is null)
            return Results.NotFound(new { message = "Item not found." });

        var resolvedPage = Math.Max(1, page ?? 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.EventId == evtId && x.OrganizationId == orgId);

        var statusValue = (status ?? "all").Trim().ToLowerInvariant();
        if (statusValue == "not_returned")
        {
            // "not_returned" = last successful action is Give (still has the item)
            var notReturnedParticipantIds = await db.ParticipantItemLogs.AsNoTracking()
                .Where(x => x.ItemId == itId && x.OrganizationId == orgId && x.ParticipantId != null && x.Result == ResultSuccess)
                .GroupBy(x => x.ParticipantId!.Value)
                .Select(g => new { ParticipantId = g.Key, LastAction = g.OrderByDescending(z => z.CreatedAt).Select(z => z.Action).FirstOrDefault() })
                .Where(x => x.LastAction == "Give")
                .Select(x => x.ParticipantId)
                .ToListAsync(ct);

            participantsQuery = participantsQuery.Where(x => notReturnedParticipantIds.Contains(x.Id));
        }
        else if (statusValue == "given")
        {
            // "given" = has at least one successful Give (includes returned)
            var everGivenParticipantIds = await db.ParticipantItemLogs.AsNoTracking()
                .Where(x => x.ItemId == itId && x.OrganizationId == orgId && x.ParticipantId != null && x.Result == ResultSuccess && x.Action == "Give")
                .Select(x => x.ParticipantId!.Value)
                .Distinct()
                .ToListAsync(ct);

            participantsQuery = participantsQuery.Where(x => everGivenParticipantIds.Contains(x.Id));
        }
        else if (statusValue == "returned")
        {
            var returnedParticipantIds = await db.ParticipantItemLogs.AsNoTracking()
                .Where(x => x.ItemId == itId && x.OrganizationId == orgId && x.ParticipantId != null && x.Result == ResultSuccess)
                .GroupBy(x => x.ParticipantId!.Value)
                .Select(g => new { ParticipantId = g.Key, LastAction = g.OrderByDescending(z => z.CreatedAt).Select(z => z.Action).FirstOrDefault() })
                .Where(x => x.LastAction == "Return")
                .Select(x => x.ParticipantId)
                .ToListAsync(ct);

            participantsQuery = participantsQuery.Where(x => returnedParticipantIds.Contains(x.Id));
        }
        else if (statusValue == "never_given")
        {
            var allParticipantIds = await db.Participants.AsNoTracking()
                .Where(x => x.EventId == evtId && x.OrganizationId == orgId)
                .Select(x => x.Id)
                .ToListAsync(ct);
            var participantsWithSuccessGiveLogs = await db.ParticipantItemLogs.AsNoTracking()
                .Where(x => x.ItemId == itId && x.OrganizationId == orgId && x.Result == ResultSuccess && x.ParticipantId != null && x.Action == "Give")
                .Select(x => x.ParticipantId!.Value)
                .Distinct()
                .ToListAsync(ct);
            var neverGivenParticipantIds = allParticipantIds.Except(participantsWithSuccessGiveLogs).ToList();
            participantsQuery = participantsQuery.Where(x => neverGivenParticipantIds.Contains(x.Id));
        }

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
                    || (x.Details.AgencyName != null && EF.Functions.ILike(x.Details.AgencyName, pattern))
                )));
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
            _ => desc ? participantsQuery.OrderByDescending(x => x.FullName) : participantsQuery.OrderBy(x => x.FullName)
        };

        var pageItems = await ordered
            .Include(x => x.Details)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var participantIds = pageItems.Select(x => x.Id).ToList();
        var lastLogs = await db.ParticipantItemLogs.AsNoTracking()
            .Where(x => x.ItemId == itId && x.ParticipantId != null && participantIds.Contains(x.ParticipantId!.Value))
            .GroupBy(x => x.ParticipantId!.Value)
            .Select(g => new
            {
                ParticipantId = g.Key,
                Log = g.OrderByDescending(x => x.CreatedAt).Select(x => new { x.Action, x.Method, x.Result, x.CreatedAt }).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.ParticipantId, x => x.Log, ct);

        var items = pageItems.Select(p =>
        {
            lastLogs.TryGetValue(p.Id, out var lastLog);
            var given = lastLog != null && lastLog.Action == "Give" && lastLog.Result == ResultSuccess;
            var lastLogDto = lastLog == null ? null : new ItemLastLogDto(lastLog.Action, lastLog.Method, lastLog.Result, DateTime.SpecifyKind(lastLog.CreatedAt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            return new ItemParticipantTableItemDto(
                p.Id,
                p.FullName,
                p.Phone,
                p.Email,
                p.TcNo,
                p.CheckInCode,
                p.Details?.RoomNo,
                p.Details?.AgencyName,
                new ItemParticipantStateDto(given, lastLogDto));
        }).ToArray();

        return Results.Ok(new ItemParticipantTableResponseDto(resolvedPage, resolvedPageSize, total, items));
    }

    private static Guid? TryGetActorUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string NormalizeAction(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Give";
        var n = new string((raw.Trim()).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return n == "return" ? "Return" : "Give";
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

    private static ParticipantItemLogEntity CreateLog(
        Guid orgId, Guid evtId, Guid itId, Guid? participantId,
        string action, string method, string result,
        Guid? actorUserId, string? actorRole, string? ipAddress, string? userAgent, DateTime createdAt)
    {
        return new ParticipantItemLogEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EventId = evtId,
            ItemId = itId,
            ParticipantId = participantId,
            Action = action,
            Method = method,
            Result = result,
            ActorUserId = actorUserId,
            ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = createdAt
        };
    }
}
