using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Tours;

internal static class ToursHandlers
{
    internal static async Task<IResult> GetTours(TripflowDbContext db, CancellationToken ct)
    {
        var tours = await db.Tours.AsNoTracking()
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => ToursHelpers.ToDto(x))
            .ToArrayAsync(ct);

        return Results.Ok(tours);
    }

    internal static async Task<IResult> CreateTour(CreateTourRequest request, TripflowDbContext db, CancellationToken ct)
    {
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
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow
        };

        db.Tours.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = ToursHelpers.ToDto(entity);
        return Results.Created($"/api/tours/{dto.Id}", dto);
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

        var portalEntity = await db.TourPortals.AsNoTracking().FirstOrDefaultAsync(x => x.TourId == id, ct);
        if (portalEntity is null)
        {
            return Results.Ok(ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(tour)));
        }

        var portal = ToursHelpers.TryDeserializePortal(portalEntity.PortalJson)
            ?? ToursHelpers.CreateDefaultPortalInfo(ToursHelpers.ToDto(tour));

        return Results.Ok(portal);
    }

    internal static async Task<IResult> SavePortal(string tourId, TourPortalInfo request, TripflowDbContext db, CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var tourExists = await db.Tours.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!tourExists)
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

        var portalEntity = await db.TourPortals.FirstOrDefaultAsync(x => x.TourId == id, ct);
        if (portalEntity is null)
        {
            portalEntity = new TourPortalEntity
            {
                TourId = id,
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

    internal static async Task<IResult> GetParticipants(string tourId, TripflowDbContext db, CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var tourExists = await db.Tours.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        var participants = await db.Participants.AsNoTracking()
            .Where(x => x.TourId == id)
            .OrderBy(x => x.FullName)
            .Select(x => new ParticipantDto(x.Id, x.FullName, x.Email, x.Phone, x.CheckInCode))
            .ToArrayAsync(ct);

        return Results.Ok(participants);
    }

    internal static async Task<IResult> CreateParticipant(string tourId, CreateParticipantRequest request, TripflowDbContext db, CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var tourExists = await db.Tours.AsNoTracking().AnyAsync(x => x.Id == id, ct);
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
            new ParticipantDto(entity.Id, entity.FullName, entity.Email, entity.Phone, entity.CheckInCode));
    }

    internal static async Task<IResult> CheckIn(string tourId, CheckInRequest request, TripflowDbContext db, CancellationToken ct)
    {
        if (!ToursHelpers.TryParseTourId(tourId, out var id, out var error))
        {
            return error!;
        }

        var tourExists = await db.Tours.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!tourExists)
        {
            return Results.NotFound(new { message = "Tour not found." });
        }

        ParticipantEntity? participant = null;

        if (request.ParticipantId.HasValue)
        {
            participant = await db.Participants
                .FirstOrDefaultAsync(x => x.Id == request.ParticipantId.Value && x.TourId == id, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var code = request.Code.Trim().ToUpperInvariant();
            participant = await db.Participants
                .FirstOrDefaultAsync(x => x.TourId == id && x.CheckInCode == code, ct);
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
            .AnyAsync(x => x.TourId == id && x.ParticipantId == participant.Id, ct);

        if (!alreadyCheckedIn)
        {
            db.CheckIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                TourId = id,
                ParticipantId = participant.Id,
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

        var arrivedCount = await db.CheckIns.AsNoTracking().CountAsync(x => x.TourId == id, ct);
        var totalCount = await db.Participants.AsNoTracking().CountAsync(x => x.TourId == id, ct);

        return Results.Ok(new CheckInResponse(participant.Id, participant.FullName, arrivedCount, totalCount));
    }
}
