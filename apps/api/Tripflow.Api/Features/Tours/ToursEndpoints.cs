using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Tours;

public static class ToursEndpoints
{
    private static readonly ConcurrentDictionary<Guid, Tour> Tours = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Participant>> ParticipantsByTour = new();
    private static readonly ConcurrentDictionary<Guid, TourPortalInfo> PortalByTour = new();

    public static IEndpointRouteBuilder MapToursEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Tours");

        group.MapGet("/tours", () =>
            {
                var list = Tours.Values
                    .OrderBy(t => t.StartDate)
                    .ThenBy(t => t.Name)
                    .ToArray();

                return Results.Ok(list);
            })
            .WithSummary("List tours")
            .WithDescription("Returns all tours in the in-memory store.")
            .WithOpenApi();

        group.MapPost("/tours", (CreateTourRequest request) =>
            {
                if (request is null)
                {
                    return ValidationError("Request body is required.");
                }

                var name = request.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ValidationError("Name is required.");
                }

                if (!TryParseDate(request.StartDate, out var startDate))
                {
                    return ValidationError("Start date must be in YYYY-MM-DD format.");
                }

                if (!TryParseDate(request.EndDate, out var endDate))
                {
                    return ValidationError("End date must be in YYYY-MM-DD format.");
                }

                if (endDate < startDate)
                {
                    return ValidationError("End date must be on or after start date.");
                }

                var tour = new Tour(Guid.NewGuid(), name, request.StartDate!, request.EndDate!);
                if (!Tours.TryAdd(tour.Id, tour))
                {
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }

                ParticipantsByTour.TryAdd(tour.Id, new ConcurrentDictionary<Guid, Participant>());
                PortalByTour.TryAdd(tour.Id, CreateDefaultPortalInfo(tour));

                return Results.Created($"/api/tours/{tour.Id}", tour);
            })
            .WithSummary("Create tour")
            .WithDescription("Creates a new tour with a name and date range.")
            .WithOpenApi();

        group.MapGet("/tours/{tourId}", (string tourId) =>
            {
                if (!TryGetTour(tourId, out var tour, out var error))
                {
                    return error!;
                }

                return Results.Ok(tour);
            })
            .WithSummary("Get tour")
            .WithDescription("Returns a tour by id.")
            .WithOpenApi();

        group.MapGet("/tours/{tourId}/portal", (string tourId) =>
            {
                if (!TryGetTour(tourId, out var tour, out var error))
                {
                    return error!;
                }

                var portal = PortalByTour.GetOrAdd(tour.Id, _ => CreateDefaultPortalInfo(tour));

                return Results.Ok(portal);
            })
            .WithSummary("Get tour portal")
            .WithDescription("Returns portal content for a tour.")
            .WithOpenApi();

        group.MapPut("/tours/{tourId}/portal", (string tourId, TourPortalInfo request) =>
            {
                if (!TryGetTour(tourId, out var tour, out var error))
                {
                    return error!;
                }

                if (request is null)
                {
                    return ValidationError("Request body is required.");
                }

                if (request.Meeting is null)
                {
                    return ValidationError("Meeting details are required.");
                }

                if (string.IsNullOrWhiteSpace(request.Meeting.Time))
                {
                    return ValidationError("Meeting time is required.");
                }

                if (string.IsNullOrWhiteSpace(request.Meeting.Place))
                {
                    return ValidationError("Meeting place is required.");
                }

                if (string.IsNullOrWhiteSpace(request.Meeting.MapsUrl))
                {
                    return ValidationError("Meeting maps URL is required.");
                }

                PortalByTour[tour.Id] = request;

                return Results.Ok(request);
            })
            .WithSummary("Update tour portal")
            .WithDescription("Updates portal content for a tour.")
            .WithOpenApi();

        group.MapGet("/tours/{tourId}/participants", (string tourId) =>
            {
                if (!TryGetTour(tourId, out var tour, out var error))
                {
                    return error!;
                }

                var participants = ParticipantsByTour
                    .GetOrAdd(tour.Id, _ => new ConcurrentDictionary<Guid, Participant>())
                    .Values
                    .OrderBy(p => p.FullName)
                    .ToArray();

                return Results.Ok(participants);
            })
            .WithSummary("List participants")
            .WithDescription("Returns all participants for a tour.")
            .WithOpenApi();

        group.MapPost("/tours/{tourId}/participants", (string tourId, CreateParticipantRequest request) =>
            {
                if (!TryGetTour(tourId, out var tour, out var error))
                {
                    return error!;
                }

                if (request is null)
                {
                    return ValidationError("Request body is required.");
                }

                var fullName = request.FullName?.Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return ValidationError("Full name is required.");
                }

                var participant = new Participant(Guid.NewGuid(), fullName, request.Email?.Trim(), request.Phone?.Trim());
                var participants = ParticipantsByTour.GetOrAdd(tour.Id, _ => new ConcurrentDictionary<Guid, Participant>());
                participants.TryAdd(participant.Id, participant);

                return Results.Created($"/api/tours/{tour.Id}/participants/{participant.Id}", participant);
            })
            .WithSummary("Add participant")
            .WithDescription("Adds a participant to a tour.")
            .WithOpenApi();

        return app;
    }

    private static bool TryParseDate(string? value, out DateOnly date)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryGetTour(string tourIdValue, out Tour tour, out IResult? error)
    {
        if (!Guid.TryParse(tourIdValue, out var tourId))
        {
            tour = default!;
            error = ValidationError("Invalid tour id.");
            return false;
        }

        if (!Tours.TryGetValue(tourId, out tour))
        {
            error = Results.NotFound(new { message = "Tour not found." });
            return false;
        }

        error = null;
        return true;
    }

    private static IResult ValidationError(string message)
    {
        return Results.BadRequest(new { message });
    }

    private static TourPortalInfo CreateDefaultPortalInfo(Tour tour)
    {
        var meeting = new MeetingInfo(
            "08:30",
            "Hotel lobby - Grand Central Hotel",
            "https://maps.google.com/?q=Grand+Central+Hotel",
            $"Welcome to {tour.Name}. Please arrive 15 minutes early.");

        var links = new[]
        {
            new LinkInfo("Tour info pack", "https://example.com/tripflow/tour-pack"),
            new LinkInfo("Emergency contacts", "https://example.com/tripflow/emergency"),
            new LinkInfo("Feedback form", "https://example.com/tripflow/feedback")
        };

        var days = new[]
        {
            new DayPlan(1, "Arrival and orientation", new[]
            {
                "Hotel check-in and welcome briefing",
                "City center walk",
                "Group dinner"
            }),
            new DayPlan(2, "Signature sights", new[]
            {
                "Morning guided tour",
                "Free time for lunch",
                "Museum visit"
            }),
            new DayPlan(3, "Local experiences", new[]
            {
                "Market visit",
                "Optional activities",
                "Closing meetup"
            })
        };

        var notes = new[]
        {
            "Bring a reusable water bottle.",
            "Wear comfortable walking shoes.",
            "Share dietary restrictions with the guide."
        };

        return new TourPortalInfo(meeting, links, days, notes);
    }

    public sealed record CreateTourRequest(string? Name, string? StartDate, string? EndDate);
    public sealed record Tour(Guid Id, string Name, string StartDate, string EndDate);
    public sealed record TourPortalInfo(MeetingInfo Meeting, LinkInfo[] Links, DayPlan[] Days, string[] Notes);
    public sealed record MeetingInfo(string Time, string Place, string MapsUrl, string Note);
    public sealed record LinkInfo(string Label, string Url);
    public sealed record DayPlan(int Day, string Title, string[] Items);
    public sealed record CreateParticipantRequest(string? FullName, string? Email, string? Phone);
    public sealed record Participant(Guid Id, string FullName, string? Email, string? Phone);
}
