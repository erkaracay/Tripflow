using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

internal static class PortalAccessHandlers
{
    internal static Task<IResult> VerifyAccess(
        PortalAccessVerifyRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        return Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status410Gone));
    }

    internal static Task<IResult> ConfirmAccess(
        PortalAccessConfirmRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        return Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status410Gone));
    }

    internal static Task<IResult> GetMe(
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        return Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status410Gone));
    }

    internal static async Task<IResult> GetParticipantAccess(
        string eventId,
        string participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var participant = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var access = await db.ParticipantAccesses
            .Where(x => x.ParticipantId == participant.Id && x.OrganizationId == orgId && x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (access is null)
        {
            access = await CreateAccessAsync(db, participant, orgId, DateTime.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }

        var token = PortalAccessHelpers.BuildToken(access.Id, access.Secret);
        var isLocked = participant.PortalLockedUntil.HasValue && participant.PortalLockedUntil.Value > DateTime.UtcNow;

        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orgId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new ParticipantPortalAccessResponse(
            token,
            isLocked,
            participant.PortalLockedUntil,
            participant.PortalFailedAttempts,
            BuildPolicy(org)));
    }

    internal static async Task<IResult> ResetParticipantAccess(
        string eventId,
        string participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var eventGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return EventsHelpers.BadRequest("Invalid participant id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var participant = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.EventId == eventGuid && x.OrganizationId == orgId, ct);

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var now = DateTime.UtcNow;
        var accessTokens = await db.ParticipantAccesses
            .Where(x => x.ParticipantId == participant.Id && x.OrganizationId == orgId && x.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in accessTokens)
        {
            token.RevokedAt = now;
        }

        var sessions = await db.PortalSessions
            .Where(x => x.ParticipantId == participant.Id && x.OrganizationId == orgId)
            .ToListAsync(ct);
        if (sessions.Count > 0)
        {
            db.PortalSessions.RemoveRange(sessions);
        }

        participant.PortalFailedAttempts = 0;
        participant.PortalLockedUntil = null;
        participant.PortalLastFailedAt = null;

        var access = await CreateAccessAsync(db, participant, orgId, now, ct);
        await db.SaveChangesAsync(ct);

        var tokenValue = PortalAccessHelpers.BuildToken(access.Id, access.Secret);
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orgId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new ParticipantPortalAccessResponse(
            tokenValue,
            false,
            null,
            0,
            BuildPolicy(org)));
    }

    private static async Task<ParticipantAccessEntity> CreateAccessAsync(
        TripflowDbContext db,
        ParticipantEntity participant,
        Guid orgId,
        DateTime now,
        CancellationToken ct)
    {
        var currentVersion = await db.ParticipantAccesses
            .Where(x => x.ParticipantId == participant.Id && x.OrganizationId == orgId)
            .MaxAsync(x => (int?)x.Version, ct) ?? 0;

        var secret = PortalAccessHelpers.GenerateSecret();
        var tokenId = Guid.NewGuid();
        var entity = new ParticipantAccessEntity
        {
            Id = tokenId,
            OrganizationId = orgId,
            ParticipantId = participant.Id,
            Version = currentVersion + 1,
            Secret = secret,
            SecretHash = PortalAccessHelpers.HashSecret(secret),
            CreatedAt = now,
            RevokedAt = null
        };

        db.ParticipantAccesses.Add(entity);
        return entity;
    }

    private static PortalAccessPolicy BuildPolicy(OrganizationEntity organization)
        => new(
            organization.RequireLast4ForQr,
            organization.RequireLast4ForPortal,
            PortalAccessHelpers.MaxAttempts,
            (int)PortalAccessHelpers.LockDuration.TotalMinutes);

    private static PortalParticipantSummary BuildParticipantSummary(ParticipantEntity participant)
    {
        var name = participant.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new PortalParticipantSummary("Participant", !string.IsNullOrWhiteSpace(participant.Phone));
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return new PortalParticipantSummary(parts[0], !string.IsNullOrWhiteSpace(participant.Phone));
        }

        var masked = $"{parts[0]} {parts[1][0]}.";
        return new PortalParticipantSummary(masked, !string.IsNullOrWhiteSpace(participant.Phone));
    }

    private static async Task<EventPortalInfo?> GetPortalInfoAsync(
        TripflowDbContext db,
        Guid eventId,
        Guid organizationId,
        CancellationToken ct)
    {
        var eventEntity = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eventId, ct);
        if (eventEntity is null)
        {
            return null;
        }

        var portalEntity = await db.EventPortals.FirstOrDefaultAsync(x => x.EventId == eventId, ct);
        if (portalEntity is null)
        {
            var fallback = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            var json = System.Text.Json.JsonSerializer.Serialize(fallback, EventsHelpers.JsonOptions);

            db.EventPortals.Add(new EventPortalEntity
            {
                EventId = eventId,
                OrganizationId = organizationId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            return fallback;
        }

        var portal = EventsHelpers.TryDeserializePortal(portalEntity.PortalJson);
        if (portal is null)
        {
            portal = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(eventEntity));
            portalEntity.PortalJson = System.Text.Json.JsonSerializer.Serialize(portal, EventsHelpers.JsonOptions);
            portalEntity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return portal;
    }
}
