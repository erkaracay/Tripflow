using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Features.Tours;

namespace Tripflow.Api.Features.Portal;

internal static class PortalAccessHandlers
{
    internal static async Task<IResult> VerifyAccess(
        PortalAccessVerifyRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Pt))
        {
            return Results.BadRequest(new { message = "pt token is required." });
        }

        if (!Guid.TryParse(request.TourId, out var tourId))
        {
            return Results.BadRequest(new { message = "tourId is required." });
        }

        if (!PortalAccessHelpers.TryParseToken(request.Pt, out var tokenId, out var secret))
        {
            return Results.NotFound();
        }

        var access = await db.ParticipantAccesses
            .Include(x => x.Participant)
            .FirstOrDefaultAsync(x => x.Id == tokenId && x.RevokedAt == null, ct);

        if (access is null || !PortalAccessHelpers.SecretMatches(secret, access.SecretHash))
        {
            return Results.NotFound();
        }

        var participant = access.Participant;
        if (participant is null)
        {
            return Results.NotFound();
        }

        if (participant.TourId != tourId)
        {
            return Results.NotFound();
        }

        var org = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == participant.OrganizationId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        var now = DateTime.UtcNow;
        var lockedUntil = participant.PortalLockedUntil;
        var isLocked = lockedUntil.HasValue && lockedUntil.Value > now;
        var lockedForSeconds = isLocked ? (int)Math.Ceiling((lockedUntil!.Value - now).TotalSeconds) : 0;
        var attemptsRemaining = isLocked
            ? 0
            : Math.Max(0, PortalAccessHelpers.MaxAttempts - participant.PortalFailedAttempts);

        var policy = BuildPolicy(org);
        var portal = await GetPortalInfoAsync(db, participant.TourId, participant.OrganizationId, ct);
        if (portal is null)
        {
            return Results.NotFound();
        }

        var response = new PortalAccessVerifyResponse(
            participant.TourId,
            portal,
            policy,
            BuildParticipantSummary(participant),
            PortalAccessHelpers.BuildPhoneHint(participant.Phone),
            isLocked,
            lockedForSeconds,
            attemptsRemaining);

        return Results.Ok(response);
    }

    internal static async Task<IResult> ConfirmAccess(
        PortalAccessConfirmRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Pt))
        {
            return Results.BadRequest(new { message = "pt token is required." });
        }

        if (!Guid.TryParse(request.TourId, out var tourId))
        {
            return Results.BadRequest(new { message = "tourId is required." });
        }

        if (!PortalAccessHelpers.TryParseToken(request.Pt, out var tokenId, out var secret))
        {
            return Results.NotFound();
        }

        var access = await db.ParticipantAccesses
            .Include(x => x.Participant)
            .FirstOrDefaultAsync(x => x.Id == tokenId && x.RevokedAt == null, ct);

        if (access is null || !PortalAccessHelpers.SecretMatches(secret, access.SecretHash))
        {
            return Results.NotFound();
        }

        var participant = access.Participant;
        if (participant is null)
        {
            return Results.NotFound();
        }

        if (participant.TourId != tourId)
        {
            return Results.NotFound();
        }

        var org = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == participant.OrganizationId, ct);
        if (org is null)
        {
            return Results.NotFound();
        }

        var policy = BuildPolicy(org);
        var requiresLast4 = policy.RequireLast4ForPortal || policy.RequireLast4ForQr;
        var now = DateTime.UtcNow;

        if (requiresLast4)
        {
            if (participant.PortalLockedUntil.HasValue && participant.PortalLockedUntil.Value > now)
            {
                return Results.Json(new { message = "Too many attempts. Try later." }, statusCode: StatusCodes.Status429TooManyRequests);
            }

            var phoneLast4 = PortalAccessHelpers.ExtractLast4(participant.Phone);
            if (string.IsNullOrWhiteSpace(phoneLast4))
            {
                return Results.BadRequest(new { message = "Participant phone is required." });
            }

            var providedLast4 = PortalAccessHelpers.ExtractLast4(request.Last4);
            if (string.IsNullOrWhiteSpace(providedLast4) || !string.Equals(phoneLast4, providedLast4, StringComparison.Ordinal))
            {
                participant.PortalFailedAttempts += 1;
                participant.PortalLastFailedAt = now;

                if (participant.PortalFailedAttempts >= PortalAccessHelpers.MaxAttempts)
                {
                    participant.PortalLockedUntil = now.Add(PortalAccessHelpers.LockDuration);
                }

                await db.SaveChangesAsync(ct);
                return Results.Unauthorized();
            }

            participant.PortalFailedAttempts = 0;
            participant.PortalLockedUntil = null;
            participant.PortalLastFailedAt = null;
        }

        var session = new PortalSessionEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = participant.OrganizationId,
            ParticipantId = participant.Id,
            CreatedAt = now,
            ExpiresAt = now.Add(PortalAccessHelpers.SessionLifetime)
        };

        db.PortalSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new PortalAccessConfirmResponse(session.Id.ToString(), session.ExpiresAt, policy, BuildParticipantSummary(participant)));
    }

    internal static async Task<IResult> GetMe(
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var sessionHeader = httpContext.Request.Headers["X-Portal-Session"].FirstOrDefault();
        if (!Guid.TryParse(sessionHeader, out var sessionId))
        {
            return Results.Unauthorized();
        }

        var session = await db.PortalSessions
            .Include(x => x.Participant)
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);

        if (session is null || session.ExpiresAt <= DateTime.UtcNow)
        {
            return Results.Unauthorized();
        }

        var participant = session.Participant;
        if (participant is null)
        {
            return Results.Unauthorized();
        }

        var org = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == participant.OrganizationId, ct);
        if (org is null)
        {
            return Results.Unauthorized();
        }

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.ParticipantId == participant.Id && x.OrganizationId == participant.OrganizationId, ct);

        return Results.Ok(new PortalAccessMeResponse(
            participant.TourId,
            participant.Id,
            participant.FullName,
            participant.CheckInCode,
            arrived,
            BuildPolicy(org)));
    }

    internal static async Task<IResult> GetParticipantAccess(
        string tourId,
        string participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var tourGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return ToursHelpers.BadRequest("Invalid participant id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var participant = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.TourId == tourGuid && x.OrganizationId == orgId, ct);

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
        string tourId,
        string participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var tourGuid, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return ToursHelpers.BadRequest("Invalid participant id.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var participant = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.TourId == tourGuid && x.OrganizationId == orgId, ct);

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

    private static async Task<TourPortalInfo?> GetPortalInfoAsync(
        TripflowDbContext db,
        Guid tourId,
        Guid organizationId,
        CancellationToken ct)
    {
        var tour = await db.Tours.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tourId, ct);
        if (tour is null)
        {
            return null;
        }

        var portalEntity = await db.TourPortals.FirstOrDefaultAsync(x => x.TourId == tourId, ct);
        if (portalEntity is null)
        {
            var fallback = ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(tour));
            var json = System.Text.Json.JsonSerializer.Serialize(fallback, ToursHelpers.JsonOptions);

            db.TourPortals.Add(new TourPortalEntity
            {
                TourId = tourId,
                OrganizationId = organizationId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            return fallback;
        }

        var portal = ToursHelpers.TryDeserializePortal(portalEntity.PortalJson);
        if (portal is null)
        {
            portal = ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(tour));
            portalEntity.PortalJson = System.Text.Json.JsonSerializer.Serialize(portal, ToursHelpers.JsonOptions);
            portalEntity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return portal;
    }
}
