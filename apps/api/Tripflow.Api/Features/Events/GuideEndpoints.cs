using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.Events;

public static class GuideEndpoints
{
    public static IEndpointRouteBuilder MapGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/guide")
            .WithTags("Guide")
            .RequireAuthorization("GuideOnly");

        group.MapGet("/events", GuideHandlers.GetEvents)
            .WithSummary("Guide events")
            .WithDescription("Returns events assigned to the current guide.")
            .Produces<EventListItemDto[]>(StatusCodes.Status200OK);

        group.MapGet("/events/{eventId}/participants", GuideHandlers.GetParticipants)
            .WithSummary("Guide participants")
            .WithDescription("Returns participants for a guide's event.")
            .Produces<ParticipantDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/participants/resolve", GuideHandlers.ResolveParticipantByCode)
            .WithSummary("Resolve participant by check-in code")
            .WithDescription("Resolves a participant using check-in code for a guide event.")
            .Produces<ParticipantResolveDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/checkins/summary", GuideHandlers.GetCheckInSummary)
            .WithSummary("Guide check-in summary")
            .WithDescription("Returns arrived/total counts for a guide's event.")
            .Produces<CheckInSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/checkins", GuideHandlers.CheckInByCode)
            .WithSummary("Guide check-in by code")
            .WithDescription("Marks participant as arrived using checkInCode.")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/checkins/undo", GuideHandlers.UndoCheckIn)
            .WithSummary("Guide undo check-in")
            .WithDescription("Reverts a participant check-in for a guide event.")
            .Produces<CheckInUndoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/checkins/reset-all", GuideHandlers.ResetAllCheckIns)
            .WithSummary("Guide reset all check-ins")
            .WithDescription("Removes all check-ins for a guide event.")
            .Produces<ResetAllCheckInsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
