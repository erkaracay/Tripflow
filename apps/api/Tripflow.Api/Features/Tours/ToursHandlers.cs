using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Tours;

internal static class ToursHandlers
{
    internal static async Task<IResult> GetTours(HttpContext httpContext, TripflowDbContext db, CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var error))
        {
            return error!;
        }

        var tours = await db.Tours.AsNoTracking()
            .Where(x => x.OrganizationId == orgId)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new TourListItemDto(
                x.Id,
                x.Name,
                x.StartDate.ToString("yyyy-MM-dd"),
                x.EndDate.ToString("yyyy-MM-dd"),
                db.CheckIns.Count(c => c.TourId == x.Id),
                db.Participants.Count(p => p.TourId == x.Id),
                x.GuideUserId))
            .ToArrayAsync(ct);

        return Results.Ok(tours);
    }

    internal static async Task<IResult> CreateTour(
        CreateTourRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToursHelpers.BadRequest("Name is required.");
        }

        if (!ToursHelpers.TryParseDate(request.StartDate, out var startDate))
        {
            return ToursHelpers.BadRequest("Start date must be in YYYY-MM-DD format.");
        }

        if (!ToursHelpers.TryParseDate(request.EndDate, out var endDate))
        {
            return ToursHelpers.BadRequest("End date must be in YYYY-MM-DD format.");
        }

        if (endDate < startDate)
        {
            return ToursHelpers.BadRequest("End date must be on or after start date.");
        }

        var entity = new TourEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow
        };

        var portalJson = System.Text.Json.JsonSerializer.Serialize(
            ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(entity)),
            ToursHelpers.JsonOptions);

        db.Tours.Add(entity);
        db.TourPortals.Add(new TourPortalEntity
        {
            TourId = entity.Id,
            OrganizationId = orgId,
            PortalJson = portalJson,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var dto = ToursHelpers.ToDto(entity);
        return Results.Created($"/api/tours/{dto.Id}", dto);
    }

    internal static async Task<IResult> UpdateTour(
        string tourId,
        UpdateTourRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (request is null)
        {
            return ToursHelpers.BadRequest("Request body is required.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToursHelpers.BadRequest("Name is required.");
        }

        if (!ToursHelpers.TryParseDate(request.StartDate, out var startDate))
        {
            return ToursHelpers.BadRequest("Start date must be in YYYY-MM-DD format.");
        }

        if (!ToursHelpers.TryParseDate(request.EndDate, out var endDate))
        {
            return ToursHelpers.BadRequest("End date must be in YYYY-MM-DD format.");
        }

        if (endDate < startDate)
        {
            return ToursHelpers.BadRequest("End date must be on or after start date.");
        }

        var entity = await db.Tours.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        entity.Name = name;
        entity.StartDate = startDate;
        entity.EndDate = endDate;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToursHelpers.ToDto(entity));
    }

    internal static async Task<IResult> GetTour(string tourId, TripflowDbContext db, CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var entity = await db.Tours.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        return Results.Ok(ToursHelpers.ToDto(entity));
    }

    internal static async Task<IResult> GetPortal(string tourId, TripflowDbContext db, CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var tour = await db.Tours.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tour is null)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var portalEntity = await db.TourPortals.FirstOrDefaultAsync(x => x.TourId == id, ct);
        if (portalEntity is null)
        {
            var fallback = ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(tour));
            var json = System.Text.Json.JsonSerializer.Serialize(fallback, ToursHelpers.JsonOptions);

            db.TourPortals.Add(new TourPortalEntity
            {
                TourId = id,
                OrganizationId = tour.OrganizationId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            return Results.Ok(fallback);
        }

        var portal = ToursHelpers.TryDeserializePortal(portalEntity.PortalJson);
        if (portal is null)
        {
            portal = ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(tour));
            portalEntity.PortalJson = System.Text.Json.JsonSerializer.Serialize(portal, ToursHelpers.JsonOptions);
            portalEntity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(portal);
    }

    internal static async Task<IResult> VerifyCheckInCode(
        string tourId,
        VerifyCheckInCodeRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var raw = request?.CheckInCode ?? string.Empty;
        var normalized = raw.Trim().ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);

        if (normalized.Length != 8)
        {
            return Results.Ok(new VerifyCheckInCodeResponse(false, null));
        }

        var tourExists = await db.Tours.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!tourExists)
        {
            return Results.Ok(new VerifyCheckInCodeResponse(false, null));
        }

        var isValid = await db.Participants.AsNoTracking()
            .AnyAsync(p => p.TourId == id && p.CheckInCode == normalized, ct);

        return Results.Ok(new VerifyCheckInCodeResponse(isValid, isValid ? normalized : null));
    }

    internal static async Task<IResult> SavePortal(
        string tourId,
        TourPortalInfo request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var tour = await db.Tours.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (tour is null)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        if (request.Meeting is null)
        {
            return ToursHelpers.BadRequest("Meeting details are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Meeting.Time) ||
            string.IsNullOrWhiteSpace(request.Meeting.Place) ||
            string.IsNullOrWhiteSpace(request.Meeting.MapsUrl))
        {
            return ToursHelpers.BadRequest("Meeting time/place/mapsUrl are required.");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(request, ToursHelpers.JsonOptions);

        var portalEntity = await db.TourPortals
            .FirstOrDefaultAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);
        if (portalEntity is null)
        {
            portalEntity = new TourPortalEntity
            {
                TourId = id,
                OrganizationId = orgId,
                PortalJson = json,
                UpdatedAt = DateTime.UtcNow
            };
            db.TourPortals.Add(portalEntity);
        }
        else
        {
            portalEntity.PortalJson = json;
            portalEntity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(request);
    }

    internal static async Task<IResult> AssignGuide(
        string tourId,
        AssignGuideRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        if (request is null || !request.GuideUserId.HasValue || request.GuideUserId == Guid.Empty)
        {
            return ToursHelpers.BadRequest("Guide user id is required.");
        }

        var tour = await db.Tours.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (tour is null)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var guideId = request.GuideUserId.Value;
        var guideExists = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == guideId && x.Role == "Guide" && x.OrganizationId == orgId, ct);

        if (!guideExists)
        {
            return ToursHelpers.BadRequest("Guide user not found.");
        }

        tour.GuideUserId = guideId;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { tourId = id, guideUserId = guideId });
    }

    internal static async Task<IResult> GetParticipants(
        string tourId,
        string? query,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var participantsQuery = db.Participants.AsNoTracking()
            .Where(x => x.TourId == id && x.OrganizationId == orgId);

        var search = query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            participantsQuery = participantsQuery.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)));
        }

        var checkinsQuery = db.CheckIns.AsNoTracking()
            .Where(x => x.TourId == id && x.OrganizationId == orgId);

        var participants = await participantsQuery
            .OrderBy(x => x.FullName)
            .GroupJoin(
                checkinsQuery,
                participant => participant.Id,
                checkIn => checkIn.ParticipantId,
                (participant, checkIns) => new ParticipantDto(
                    participant.Id,
                    participant.FullName,
                    participant.Email,
                    participant.Phone,
                    participant.CheckInCode,
                    checkIns.Any()))
            .ToArrayAsync(ct);

        return Results.Ok(participants);
    }

    internal static async Task<IResult> CreateParticipant(
        string tourId,
        CreateParticipantRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return ToursHelpers.BadRequest("Full name is required.");
        }

        var code = await ToursHelpers.GenerateUniqueCheckInCodeAsync(db, ct);
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var entity = new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            TourId = id,
            OrganizationId = orgId,
            FullName = fullName,
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            CheckInCode = code,
            CreatedAt = DateTime.UtcNow
        };

        db.Participants.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Participant could not be created. Try again." });
        }

        return Results.Created($"/api/tours/{id}/participants/{entity.Id}",
            new ParticipantDto(entity.Id, entity.FullName, entity.Email, entity.Phone, entity.CheckInCode, false));
    }

    internal static async Task<IResult> UpdateParticipant(
        string tourId,
        string participantId,
        UpdateParticipantRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return ToursHelpers.BadRequest("Invalid participant id.");
        }

        if (request is null)
        {
            return ToursHelpers.BadRequest("Request body is required.");
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var entity = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.TourId == id && x.OrganizationId == orgId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return ToursHelpers.BadRequest("Full name is required.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        entity.FullName = fullName;
        entity.Email = email;
        entity.Phone = phone;

        await db.SaveChangesAsync(ct);

        var arrived = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.TourId == id && x.ParticipantId == entity.Id && x.OrganizationId == orgId, ct);

        return Results.Ok(new ParticipantDto(entity.Id, entity.FullName, entity.Email, entity.Phone, entity.CheckInCode, arrived));
    }

    internal static async Task<IResult> DeleteParticipant(
        string tourId,
        string participantId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
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

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var entity = await db.Participants.FirstOrDefaultAsync(
            x => x.Id == participantGuid && x.TourId == id && x.OrganizationId == orgId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var checkIn = await db.CheckIns.FirstOrDefaultAsync(
            x => x.TourId == id && x.ParticipantId == participantGuid && x.OrganizationId == orgId, ct);
        if (checkIn is not null)
        {
            db.CheckIns.Remove(checkIn);
        }

        db.Participants.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    internal static async Task<IResult> CheckIn(
        string tourId,
        CheckInRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await CheckInForOrg(orgId, tourId, request, db, ct);
    }

    internal static async Task<IResult> CheckInForOrg(
        Guid orgId,
        string tourId,
        CheckInRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        ParticipantEntity? participant = null;

        if (request.ParticipantId.HasValue)
        {
            participant = await db.Participants
                .FirstOrDefaultAsync(x => x.Id == request.ParticipantId.Value && x.TourId == id && x.OrganizationId == orgId, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var code = request.Code.Trim().ToUpperInvariant();
            participant = await db.Participants
                .FirstOrDefaultAsync(x => x.TourId == id && x.CheckInCode == code && x.OrganizationId == orgId, ct);
        }
        else
        {
            return ToursHelpers.BadRequest("Provide either participantId or code.");
        }

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var method = (request.Method ?? "manual").Trim().ToLowerInvariant();
        if (method is not ("manual" or "qr"))
        {
            method = "manual";
        }

        var alreadyCheckedIn = await db.CheckIns.AsNoTracking()
            .AnyAsync(x => x.TourId == id && x.ParticipantId == participant.Id && x.OrganizationId == orgId, ct);

        if (!alreadyCheckedIn)
        {
            db.CheckIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                TourId = id,
                ParticipantId = participant.Id,
                OrganizationId = orgId,
                CheckedInAt = DateTime.UtcNow,
                Method = method
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            { }
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInResponse(participant.Id, participant.FullName, alreadyCheckedIn, arrivedCount, totalCount));
    }

    internal static async Task<IResult> UndoCheckIn(
        string tourId,
        CheckInUndoRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await UndoCheckInForOrg(orgId, tourId, request, db, ct);
    }

    internal static async Task<IResult> UndoCheckInForOrg(
        Guid orgId,
        string tourId,
        CheckInUndoRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (request is null)
        {
            return ToursHelpers.BadRequest("Request body is required.");
        }

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        ParticipantEntity? participant = null;

        if (request.ParticipantId.HasValue)
        {
            participant = await db.Participants.FirstOrDefaultAsync(
                x => x.Id == request.ParticipantId.Value && x.TourId == id && x.OrganizationId == orgId, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.CheckInCode))
        {
            var code = request.CheckInCode.Trim().ToUpperInvariant();
            if (code.Length != 8)
            {
                return ToursHelpers.BadRequest("Check-in code must be 8 characters.");
            }

            participant = await db.Participants.FirstOrDefaultAsync(
                x => x.TourId == id && x.CheckInCode == code && x.OrganizationId == orgId, ct);
        }
        else
        {
            return ToursHelpers.BadRequest("Provide participantId or checkInCode.");
        }

        if (participant is null)
        {
            return Results.NotFound(new { message = "Participant not found." });
        }

        var checkIn = await db.CheckIns.FirstOrDefaultAsync(
            x => x.TourId == id && x.ParticipantId == participant.Id && x.OrganizationId == orgId, ct);

        var alreadyUndone = checkIn is null;
        if (!alreadyUndone)
        {
            db.CheckIns.Remove(checkIn!);
            await db.SaveChangesAsync(ct);
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInUndoResponse(participant.Id, alreadyUndone, arrivedCount, totalCount));
    }

    internal static async Task<IResult> CheckInByCode(
        string tourId,
        CheckInCodeRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        return await CheckInByCodeForOrg(orgId, tourId, request, db, ct);
    }

    internal static async Task<IResult> CheckInByCodeForOrg(
        Guid orgId,
        string tourId,
        CheckInCodeRequest request,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return ToursHelpers.BadRequest("Request body is required.");
        }

        var code = request.CheckInCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return ToursHelpers.BadRequest("Check-in code is required.");
        }

        if (code.Length != 8)
        {
            return ToursHelpers.BadRequest("Check-in code must be 8 characters.");
        }

        return await CheckInForOrg(orgId, tourId, new CheckInRequest(code, null, "qr"), db, ct);
    }

    internal static async Task<IResult> GetCheckInSummary(
        string tourId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var tourExists = await db.Tours.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var arrivedCount = await db.CheckIns.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);
        var totalCount = await db.Participants.AsNoTracking()
            .CountAsync(x => x.TourId == id && x.OrganizationId == orgId, ct);

        return Results.Ok(new CheckInSummary(arrivedCount, totalCount));
    }
}
