using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.AuditLogs;

public static class AuditLogsHandlers
{
    public static async Task<IResult> GetAuditLogs(
        HttpContext httpContext,
        TripflowDbContext db,
        string? action = null,
        string? result = null,
        string? userId = null,
        string? eventId = null,
        string? targetType = null,
        string? from = null,
        string? to = null,
        string? query = null,
        int? page = null,
        int? pageSize = null,
        string? sort = null,
        string? dir = null,
        CancellationToken ct = default)
    {
        var resolvedPage = Math.Max(page ?? 1, 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);

        IQueryable<Data.Entities.AuditLogEntity> logs = db.AuditLogs.AsNoTracking();

        var role = httpContext.User.FindFirstValue("role") ?? httpContext.User.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            var selectedOrgRaw = httpContext.Request.Headers["X-Org-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(selectedOrgRaw))
            {
                if (!Guid.TryParse(selectedOrgRaw, out var selectedOrgId))
                {
                    return EventsHelpers.BadRequest("X-Org-Id must be a valid GUID.");
                }

                logs = logs.Where(x => x.OrganizationId == selectedOrgId || x.OrganizationId == null);
            }
        }
        else
        {
            var orgClaim = httpContext.User.FindFirstValue("orgId");
            if (!Guid.TryParse(orgClaim, out var organizationId))
            {
                return EventsHelpers.BadRequest("orgId claim is required.");
            }

            logs = logs.Where(x => x.OrganizationId == organizationId);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                return EventsHelpers.BadRequest("userId must be a valid GUID.");
            }

            logs = logs.Where(x => x.UserId == parsedUserId);
        }

        if (!string.IsNullOrWhiteSpace(eventId))
        {
            if (!Guid.TryParse(eventId, out var parsedEventId))
            {
                return EventsHelpers.BadRequest("eventId must be a valid GUID.");
            }

            var eventIdText = parsedEventId.ToString();
            var eventJsonFilter = $$"""{"eventId":"{{eventIdText}}"}""";
            logs = logs.Where(x =>
                (x.TargetType == "event" && x.TargetId == eventIdText)
                || (x.ExtraJson != null && EF.Functions.JsonContains(x.ExtraJson, eventJsonFilter)));
        }

        var actionPrefixes = (action ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (actionPrefixes.Length > 0)
        {
            logs = logs.Where(x => actionPrefixes.Any(prefix => x.Action.StartsWith(prefix)));
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            var normalizedResult = result.Trim().ToLowerInvariant();
            logs = logs.Where(x => x.Result == normalizedResult);
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            var normalizedTargetType = targetType.Trim();
            logs = logs.Where(x => x.TargetType == normalizedTargetType);
        }

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!TryParseLogFilterDate(from, out var fromUtc, out _))
            {
                return EventsHelpers.BadRequest("Invalid from date.");
            }

            logs = logs.Where(x => x.CreatedAt >= fromUtc);
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!TryParseLogFilterDate(to, out var toUtc, out var isDateOnly))
            {
                return EventsHelpers.BadRequest("Invalid to date.");
            }

            var toUtcExclusive = isDateOnly
                ? DateTime.SpecifyKind(toUtc.Date, DateTimeKind.Utc).AddDays(1)
                : toUtc;

            logs = logs.Where(x => x.CreatedAt < toUtcExclusive);
        }

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            logs = logs.Where(x =>
                (x.TargetId != null && EF.Functions.ILike(x.TargetId, pattern))
                || (x.ExtraJson != null && EF.Functions.ILike(x.ExtraJson, pattern)));
        }

        var total = await logs.CountAsync(ct);

        var joined =
            from log in logs
            join user in db.Users.AsNoTracking()
                on log.UserId equals (Guid?)user.Id into userJoin
            from user in userJoin.DefaultIfEmpty()
            select new { log, user };

        var sortValue = (sort ?? "createdAt").Trim().ToLowerInvariant();
        var desc = !string.Equals((dir ?? "desc").Trim(), "asc", StringComparison.OrdinalIgnoreCase);

        var ordered = sortValue switch
        {
            "action" => desc
                ? joined.OrderByDescending(x => x.log.Action).ThenByDescending(x => x.log.CreatedAt)
                : joined.OrderBy(x => x.log.Action).ThenBy(x => x.log.CreatedAt),
            "result" => desc
                ? joined.OrderByDescending(x => x.log.Result).ThenByDescending(x => x.log.CreatedAt)
                : joined.OrderBy(x => x.log.Result).ThenBy(x => x.log.CreatedAt),
            "targettype" => desc
                ? joined.OrderByDescending(x => x.log.TargetType).ThenByDescending(x => x.log.CreatedAt)
                : joined.OrderBy(x => x.log.TargetType).ThenBy(x => x.log.CreatedAt),
            "useremail" => desc
                ? joined.OrderByDescending(x => x.user != null ? x.user.Email : string.Empty).ThenByDescending(x => x.log.CreatedAt)
                : joined.OrderBy(x => x.user != null ? x.user.Email : string.Empty).ThenBy(x => x.log.CreatedAt),
            "userfullname" => desc
                ? joined.OrderByDescending(x => x.user != null ? x.user.FullName ?? string.Empty : string.Empty).ThenByDescending(x => x.log.CreatedAt)
                : joined.OrderBy(x => x.user != null ? x.user.FullName ?? string.Empty : string.Empty).ThenBy(x => x.log.CreatedAt),
            _ => desc
                ? joined.OrderByDescending(x => x.log.CreatedAt)
                : joined.OrderBy(x => x.log.CreatedAt)
        };

        var rows = await ordered
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var items = rows.Select(x => new AuditLogListItemDto(
            x.log.Id,
            DateTime.SpecifyKind(x.log.CreatedAt, DateTimeKind.Utc),
            x.log.UserId,
            x.user?.Email,
            x.user?.FullName,
            x.log.Role,
            x.log.OrganizationId,
            x.log.Action,
            x.log.TargetType,
            x.log.TargetId,
            x.log.IpAddress,
            x.log.Result,
            x.log.ExtraJson)).ToArray();

        return Results.Ok(new AuditLogListResponse(items, total, resolvedPage, resolvedPageSize));
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
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            valueUtc = parsed;
            return true;
        }

        valueUtc = default;
        return false;
    }
}
