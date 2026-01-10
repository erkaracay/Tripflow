using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Tripflow.Api.Features.Tours;

public static class ToursEndpoints
{
    public static IEndpointRouteBuilder MapToursEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Tours");

        group.MapGet("/tours", ToursHandlers.GetTours)
            .WithSummary("List tours")
            .WithDescription("Returns all tours.")
            .Produces<TourDto[]>(StatusCodes.Status200OK);

        group.MapPost("/tours", ToursHandlers.CreateTour)
            .WithSummary("Create tour")
            .WithDescription("Creates a new tour. Dates must be in YYYY-MM-DD format.")
            .Produces<TourDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["name"] = new OpenApiString("Demo Tour"),
                        ["startDate"] = new OpenApiString("2026-01-10"),
                        ["endDate"] = new OpenApiString("2026-01-12")
                    },
                    "Dates must be in YYYY-MM-DD format."
                );
                return op;
            });

        group.MapGet("/tours/{tourId}", ToursHandlers.GetTour)
            .WithSummary("Get tour")
            .WithDescription("Returns tour details by id.")
            .Produces<TourDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/tours/{tourId}/portal", ToursHandlers.GetPortal)
            .WithSummary("Get portal content")
            .WithDescription("Returns portal JSON for a tour. If none exists, returns a default template.")
            .Produces<TourPortalInfo>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/tours/{tourId}/portal", ToursHandlers.SavePortal)
            .WithSummary("Save portal content")
            .WithDescription("Upserts portal JSON for a tour.")
            .Produces<TourPortalInfo>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["meeting"] = new OpenApiObject
                        {
                            ["time"] = new OpenApiString("09:00"),
                            ["place"] = new OpenApiString("Lobby"),
                            ["mapsUrl"] = new OpenApiString("https://maps.google.com/?q=Lobby"),
                            ["note"] = new OpenApiString("15 dk erken gel.")
                        },
                        ["links"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"] = new OpenApiString("Info"),
                                ["url"] = new OpenApiString("https://example.com/info")
                            }
                        },
                        ["days"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["day"] = new OpenApiInteger(1),
                                ["title"] = new OpenApiString("Day 1"),
                                ["items"] = new OpenApiArray
                                {
                                    new OpenApiString("Start"),
                                    new OpenApiString("Walk")
                                }
                            }
                        },
                        ["notes"] = new OpenApiArray
                        {
                            new OpenApiString("Su al"),
                            new OpenApiString("Rahat ayakkabı")
                        }
                    }
                );
                return op;
            });

        group.MapGet("/tours/{tourId}/participants", ToursHandlers.GetParticipants)
            .WithSummary("List participants")
            .WithDescription("Returns participants for a tour (includes checkInCode).")
            .Produces<ParticipantDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/tours/{tourId}/participants", ToursHandlers.CreateParticipant)
            .WithSummary("Create participant")
            .WithDescription("Creates a participant and generates a checkInCode.")
            .Produces<ParticipantDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["fullName"] = new OpenApiString("Ayse Kaya"),
                        ["email"] = new OpenApiString("ayse@example.com"),
                        ["phone"] = new OpenApiString("+905551234567")
                    }
                );
                return op;
            });

        group.MapPost("/tours/{tourId}/checkin", ToursHandlers.CheckIn)
            .WithSummary("Check-in participant")
            .WithDescription("Marks participant as arrived (idempotent). Provide either participantId or code.")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["code"] = new OpenApiString("A7K3Q9ZP"),
                        ["method"] = new OpenApiString("manual")
                    }
                );
                return op;
            });

        return app;
    }

    private static void AddJsonExample(OpenApiOperation op, IOpenApiAny example, string? extraDescription = null)
    {
        if (op.RequestBody is null)
        {
            op.RequestBody = new OpenApiRequestBody();
        }

        if (extraDescription is not null)
        {
            op.RequestBody.Description = extraDescription;
        }

        if (!op.RequestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            mediaType = new OpenApiMediaType();
            op.RequestBody.Content["application/json"] = mediaType;
        }

        mediaType.Example = example;
    }
}
