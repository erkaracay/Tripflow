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

        group.MapPatch("/events/{eventId}/participants/{participantId}/will-not-attend", GuideHandlers.SetParticipantWillNotAttend)
            .WithSummary("Guide participant will-not-attend")
            .WithDescription("Marks a participant as will-not-attend for the guide's event.")
            .Produces<ParticipantWillNotAttendResponseDto>(StatusCodes.Status200OK)
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

        group.MapGet("/events/{eventId}", GuideHandlers.GetEvent)
            .WithSummary("Guide event")
            .WithDescription("Returns event details for the guide's event.")
            .Produces<EventDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/schedule", GuideHandlers.GetSchedule)
            .WithSummary("Guide schedule")
            .WithDescription("Returns schedule days and activities for a guide event.")
            .Produces<EventScheduleDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/days", GuideHandlers.GetEventDays)
            .WithSummary("Guide event days")
            .WithDescription("Returns schedule days for the event (same as admin).")
            .Produces<EventDayDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/days", GuideHandlers.CreateEventDay)
            .WithSummary("Guide create day")
            .Produces<EventDayDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/events/{eventId}/days/{dayId}", GuideHandlers.UpdateEventDay)
            .WithSummary("Guide update day")
            .Produces<EventDayDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/events/{eventId}/days/{dayId}", GuideHandlers.DeleteEventDay)
            .WithSummary("Guide delete day")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/days/{dayId}/activities", GuideHandlers.GetEventActivities)
            .WithSummary("Guide day activities")
            .Produces<EventActivityDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/days/{dayId}/activities", GuideHandlers.CreateEventActivity)
            .WithSummary("Guide create activity")
            .Produces<EventActivityDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/events/{eventId}/activities/{activityId}", GuideHandlers.UpdateEventActivity)
            .WithSummary("Guide update activity")
            .Produces<EventActivityDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/events/{eventId}/activities/{activityId}", GuideHandlers.DeleteEventActivity)
            .WithSummary("Guide delete activity")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/activities/for-checkin", GuideHandlers.GetActivitiesForCheckIn)
            .WithSummary("Guide activities for check-in")
            .WithDescription("Returns activities with RequiresCheckIn for the event.")
            .Produces<EventActivityDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/activities/{activityId}/checkins", GuideHandlers.PostActivityCheckIn)
            .WithSummary("Guide activity check-in by code")
            .Produces<ActivityCheckInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/activities/{activityId}/participants/table", GuideHandlers.GetActivityParticipantsTable)
            .WithSummary("Guide activity participants table")
            .Produces<ActivityParticipantTableResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/items", GuideHandlers.GetEventItems)
            .WithSummary("Guide event items")
            .Produces<EventItemDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventId}/items/{itemId}/actions", GuideHandlers.PostItemAction)
            .WithSummary("Guide give/return item by code")
            .Produces<ItemActionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventId}/items/{itemId}/participants/table", GuideHandlers.GetItemParticipantsTable)
            .WithSummary("Guide item participants table")
            .Produces<ItemParticipantTableResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
