using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

internal static class PortalLoginHandlers
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private static readonly ConcurrentDictionary<string, List<DateTime>> Attempts = new();

    internal static async Task<IResult> Login(
        PortalLoginRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var code = NormalizeCode(request.EventAccessCode);
        var tcNo = NormalizeTcNo(request.TcNo);

        var attemptKey = $"{code}|{GetClientIp(httpContext)}";
        if (IsRateLimited(attemptKey, out var retryAfter))
        {
            httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
            return Results.Json(new { code = "rate_limited", retryAfterSeconds = retryAfter }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "invalid_access_code_format" });
        }

        if (string.IsNullOrWhiteSpace(tcNo) || tcNo.Length != 11)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "invalid_tcno_format" });
        }

        var matchingEvents = await db.Events.AsNoTracking()
            .Where(x => x.EventAccessCode == code)
            .ToListAsync(ct);

        if (matchingEvents.Count == 0)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "invalid_event_access_code" });
        }

        if (matchingEvents.Count > 1)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "ambiguous_event_access_code" });
        }

        var eventEntity = matchingEvents[0];
        var participants = await db.Participants
            .Where(x => x.EventId == eventEntity.Id && x.OrganizationId == eventEntity.OrganizationId && x.TcNo == tcNo)
            .ToListAsync(ct);

        if (participants.Count == 0)
        {
            RegisterFailure(attemptKey);
            return Results.BadRequest(new { code = "tcno_not_found" });
        }

        if (participants.Count > 1)
        {
            RegisterFailure(attemptKey);
            return Results.Conflict(new { code = "ambiguous_tcno" });
        }

        var participant = participants[0];

        var now = DateTime.UtcNow;
        var token = PortalSessionHelpers.CreateToken();
        var tokenHash = PortalSessionHelpers.HashToken(token);
        var expiresAt = now.AddDays(7);

        var session = await db.PortalSessions.FirstOrDefaultAsync(
            x => x.EventId == eventEntity.Id && x.ParticipantId == participant.Id, ct);

        if (session is null)
        {
            session = new PortalSessionEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = participant.OrganizationId,
                EventId = eventEntity.Id,
                ParticipantId = participant.Id,
                TokenHash = tokenHash,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                LastSeenAt = now
            };
            db.PortalSessions.Add(session);
        }
        else
        {
            session.TokenHash = tokenHash;
            session.CreatedAt = now;
            session.ExpiresAt = expiresAt;
            session.LastSeenAt = now;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new PortalLoginResponse(token, expiresAt, eventEntity.Id, participant.Id));
    }

    internal static async Task<IResult> GetMe(
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var session = await PortalSessionHelpers.GetValidSessionAsync(httpContext, db, ct);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var participant = session.Participant;
        if (participant is null)
        {
            return Results.Unauthorized();
        }

        var eventEntity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == session.EventId, ct);
        if (eventEntity is null)
        {
            return Results.Unauthorized();
        }

        session.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var portal = await GetPortalInfoAsync(db, eventEntity.Id, eventEntity.OrganizationId, ct);
        if (portal is null)
        {
            return Results.NotFound();
        }

        var response = new PortalMeResponse(
            new PortalEventSummary(
                eventEntity.Id,
                eventEntity.Name,
                eventEntity.StartDate.ToString("yyyy-MM-dd"),
                eventEntity.EndDate.ToString("yyyy-MM-dd")),
            new PortalParticipantSummaryFull(
                participant.Id,
                participant.FullName,
                participant.Phone,
                participant.Email,
                participant.TcNo,
                participant.BirthDate.ToString("yyyy-MM-dd"),
                participant.Gender.ToString(),
                participant.CheckInCode),
            portal);

        return Results.Ok(response);
    }

    private static string NormalizeCode(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

    private static string NormalizeTcNo(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string GetClientIp(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static bool IsRateLimited(string key, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        var now = DateTime.UtcNow;
        var list = Attempts.GetOrAdd(key, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(x => now - x > Window);
            if (list.Count < MaxAttempts)
            {
                return false;
            }

            var oldest = list.Min();
            retryAfterSeconds = (int)Math.Ceiling((Window - (now - oldest)).TotalSeconds);
            return true;
        }
    }

    private static void RegisterFailure(string key)
    {
        var now = DateTime.UtcNow;
        var list = Attempts.GetOrAdd(key, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(x => now - x > Window);
            list.Add(now);
        }
    }

    private static async Task<EventPortalInfo?> GetPortalInfoAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        CancellationToken ct)
    {
        var portalEntity = await db.EventPortals.FirstOrDefaultAsync(x => x.EventId == eventId, ct);
        if (portalEntity is null)
        {
            var eventEntity = await db.Events.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == eventId && x.OrganizationId == organizationId, ct);
            if (eventEntity is null)
            {
                return null;
            }

            var fallback = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            var json = System.Text.Json.JsonSerializer.Serialize(fallback, EventsHelpers.JsonOptions);
            portalEntity = new EventPortalEntity
            {
                EventId = eventId,
                OrganizationId = organizationId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            };
            db.EventPortals.Add(portalEntity);
            await db.SaveChangesAsync(ct);
            return fallback;
        }

        var portal = EventsHelpers.TryDeserializePortal(portalEntity.PortalJson);
        if (portal is null)
        {
            var eventEntity = await db.Events.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == eventId && x.OrganizationId == organizationId, ct);
            if (eventEntity is null)
            {
                return null;
            }

            portal = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            portalEntity.PortalJson = System.Text.Json.JsonSerializer.Serialize(portal, EventsHelpers.JsonOptions);
            portalEntity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return portal;
    }
}
