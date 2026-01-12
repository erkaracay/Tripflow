using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Tours;

public static class GuideEndpoints
{
    public static IEndpointRouteBuilder MapGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/guide")
            .WithTags("Guide")
            .RequireAuthorization("GuideOnly");

        group.MapGet("/tours", GuideHandlers.GetTours)
            .WithSummary("Guide tours")
            .WithDescription("Returns tours assigned to the current guide.")
            .Produces<TourListItemDto[]>(StatusCodes.Status200OK);

        group.MapGet("/tours/{tourId}/participants", GuideHandlers.GetParticipants)
            .WithSummary("Guide participants")
            .WithDescription("Returns participants for a guide's tour.")
            .Produces<ParticipantDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/tours/{tourId}/checkins/summary", GuideHandlers.GetCheckInSummary)
            .WithSummary("Guide check-in summary")
            .WithDescription("Returns arrived/total counts for a guide's tour.")
            .Produces<CheckInSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/tours/{tourId}/checkins", GuideHandlers.CheckInByCode)
            .WithSummary("Guide check-in by code")
            .WithDescription("Marks participant as arrived using checkInCode.")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/tours/{tourId}/checkins/undo", GuideHandlers.UndoCheckIn)
            .WithSummary("Guide undo check-in")
            .WithDescription("Reverts a participant check-in for a guide tour.")
            .Produces<CheckInUndoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
