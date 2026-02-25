using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Tripflow.Api.Features.Portal;

namespace Tripflow.Api.Features.Events;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");
        var admin = group.RequireAuthorization("AdminOnly");

        admin.MapGet("", EventsHandlers.GetEvents)
            .WithSummary("List events")
            .WithDescription("Returns all events for the organization. Set includeArchived=true to include archived events.")
            .Produces<EventListItemDto[]>(StatusCodes.Status200OK);

        admin.MapPost("", EventsHandlers.CreateEvent)
            .WithSummary("Create event")
            .WithDescription("Creates a new event. Dates must be in YYYY-MM-DD format.")
            .Produces<EventDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["name"] = new OpenApiString("Demo Event"),
                        ["startDate"] = new OpenApiString("2026-01-10"),
                        ["endDate"] = new OpenApiString("2026-01-12")
                    },
                    "Dates must be in YYYY-MM-DD format."
                );
                return op;
            });

        admin.MapGet("/{eventId}/access-code", EventsHandlers.GetEventAccessCode)
            .WithSummary("Get event access code")
            .WithDescription("Returns the event access code used for participant login.")
            .Produces<EventAccessCodeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/access-code/regenerate", EventsHandlers.RegenerateEventAccessCode)
            .WithSummary("Regenerate event access code")
            .WithDescription("Regenerates the event access code. Existing portal sessions remain valid.")
            .Produces<EventAccessCodeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/access-code", EventsHandlers.UpdateEventAccessCode)
            .WithSummary("Update event access code")
            .WithDescription("Sets the event access code (6–10 chars, A–Z/0–9, global unique).")
            .Produces<EventAccessCodeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        admin.MapPut("/{eventId}", EventsHandlers.UpdateEvent)
            .WithSummary("Update event")
            .WithDescription("Updates event name and dates.")
            .Produces<EventDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/contacts", EventsHandlers.GetEventContacts)
            .WithSummary("Get event contacts")
            .WithDescription("Returns editable portal contact details for an event.")
            .Produces<EventContactsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/contacts", EventsHandlers.UpdateEventContacts)
            .WithSummary("Update event contacts")
            .WithDescription("Updates portal contact details for an event.")
            .Produces<EventContactsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/archive", EventsHandlers.ArchiveEvent)
            .WithSummary("Archive event")
            .WithDescription("Archives an event (soft delete).")
            .Produces<EventDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/restore", EventsHandlers.RestoreEvent)
            .WithSummary("Restore event")
            .WithDescription("Restores an archived event.")
            .Produces<EventDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/days", EventsHandlers.GetEventDays)
            .WithSummary("List event days")
            .WithDescription("Returns schedule days for the event. If none exist, auto-creates based on the event date range.")
            .Produces<EventDayDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/days", EventsHandlers.CreateEventDay)
            .WithSummary("Create event day")
            .WithDescription("Creates a new schedule day for the event.")
            .Produces<EventDayDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/days/{dayId}", EventsHandlers.UpdateEventDay)
            .WithSummary("Update event day")
            .WithDescription("Updates a schedule day for the event.")
            .Produces<EventDayDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/days/{dayId}", EventsHandlers.DeleteEventDay)
            .WithSummary("Delete event day")
            .WithDescription("Deletes a schedule day and its activities.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/days/{dayId}/activities", EventsHandlers.GetEventActivities)
            .WithSummary("List day activities")
            .WithDescription("Returns activities for a schedule day.")
            .Produces<EventActivityDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/days/{dayId}/activities", EventsHandlers.CreateEventActivity)
            .WithSummary("Create activity")
            .WithDescription("Creates a new activity for a schedule day.")
            .Produces<EventActivityDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/activities/{activityId}", EventsHandlers.UpdateEventActivity)
            .WithSummary("Update activity")
            .WithDescription("Updates an activity.")
            .Produces<EventActivityDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/activities/{activityId}", EventsHandlers.DeleteEventActivity)
            .WithSummary("Delete activity")
            .WithDescription("Deletes an activity.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/activities/for-checkin", EventsHandlers.GetActivitiesForCheckIn)
            .WithSummary("List activities with check-in enabled")
            .WithDescription("Returns activities where RequiresCheckIn is true, for the activity check-in dropdown.")
            .Produces<EventActivityDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/activities/{activityId}/checkins", ActivityCheckInHandlers.PostCheckIn)
            .WithSummary("Activity check-in by code")
            .WithDescription("Logs activity Entry/Exit by participant check-in code.")
            .Produces<ActivityCheckInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/activities/{activityId}/participants/table", ActivityCheckInHandlers.GetParticipantsTable)
            .WithSummary("Activity participants table")
            .WithDescription("Returns participants with activity check-in state and last log.")
            .Produces<ActivityParticipantTableResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPatch("/{eventId}/activities/{activityId}/participants/{participantId}/will-not-attend", ActivityCheckInHandlers.SetActivityParticipantWillNotAttend)
            .WithSummary("Activity participant will-not-attend")
            .WithDescription("Marks a participant as will-not-attend for a specific activity.")
            .Produces<ActivityParticipantWillNotAttendResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/activities/{activityId}/checkins/reset-all", ActivityCheckInHandlers.ResetAllActivityCheckIns)
            .WithSummary("Reset all activity check-ins")
            .WithDescription("Removes all activity check-ins for the specified activity.")
            .Produces<ResetAllActivityCheckInsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/items", EventItemsHandlers.GetItems)
            .WithSummary("List event items")
            .WithDescription("Returns equipment items. Use includeInactive=true to list all (for management).")
            .Produces<EventItemDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/items/{itemId}", EventItemsHandlers.UpdateItem)
            .WithSummary("Update event item")
            .Produces<EventItemDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/items/{itemId}", EventItemsHandlers.DeleteItem)
            .WithSummary("Delete event item")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/items/{itemId}/actions", EventItemsHandlers.PostAction)
            .WithSummary("Give/Return item by code")
            .WithDescription("Logs Give or Return action by participant check-in code.")
            .Produces<ItemActionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/items/{itemId}/participants/table", EventItemsHandlers.GetParticipantsTable)
            .WithSummary("Item participants table")
            .WithDescription("Returns participants with item state (given/returned) and last log.")
            .Produces<ItemParticipantTableResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/items/create", EventItemsHandlers.CreateItem)
            .WithSummary("Create event item")
            .WithDescription("Adds a new equipment item to the event.")
            .Produces<EventItemDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/purge", EventsHandlers.PurgeEvent)
            .WithSummary("Purge event")
            .WithDescription("Permanently deletes an archived event and all related data.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{eventId}", EventsHandlers.GetEvent)
            .WithSummary("Get event")
            .WithDescription("Returns event details by id.")
            .Produces<EventDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapGet("/{eventId}/portal", EventsHandlers.GetPortal)
            .WithSummary("Get portal content")
            .WithDescription("Returns portal JSON for an event. If none exists, returns a default template.")
            .Produces<EventPortalInfo>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/{eventId}/checkins/verify", EventsHandlers.VerifyCheckInCode)
            .WithSummary("Verify a participant check-in code for the event (public)")
            .Produces<VerifyCheckInCodeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .AllowAnonymous()
            .WithOpenApi();

        admin.MapPut("/{eventId}/portal", EventsHandlers.SavePortal)
            .WithSummary("Save portal content")
            .WithDescription("Upserts portal JSON for an event.")
            .Produces<EventPortalInfo>(StatusCodes.Status200OK)
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

        admin.MapPut("/{eventId}/guides", EventsHandlers.AssignGuides)
            .WithSummary("Assign guides")
            .WithDescription("Assigns guide users to the event. Replaces all existing guide assignments.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["guideUserId"] = new OpenApiString("00000000-0000-0000-0000-000000000000")
                    }
                );
                return op;
            });

        admin.MapGet("/{eventId}/participants", EventsHandlers.GetParticipants)
            .WithSummary("List participants")
            .WithDescription("Returns participants for an event (includes checkInCode and arrived flag).")
            .Produces<ParticipantDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/participants/table", EventsHandlers.GetParticipantsTable)
            .WithSummary("Participants table")
            .WithDescription("Returns participants for an event with pagination and search.")
            .Produces<ParticipantTableResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/participants/{participantId:guid}", EventsHandlers.GetParticipantProfile)
            .WithSummary("Participant profile")
            .WithDescription("Returns participant details for an event (includes arrived status).")
            .Produces<ParticipantProfileDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/participants/import/template", ParticipantImportHandlers.DownloadImportTemplate)
            .WithSummary("Download participant import template")
            .WithDescription("Downloads a CSV or XLSX import template for participants.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/participants/import", ParticipantImportHandlers.ImportParticipants)
            .WithSummary("Import participants")
            .WithDescription("Imports participants from CSV/XLSX. Use mode=apply or mode=dryrun.")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .Produces<ParticipantImportReport>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/checkins/summary", EventsHandlers.GetCheckInSummary)
            .WithSummary("Check-in summary")
            .WithDescription("Returns arrived/total counts for an event.")
            .Produces<CheckInSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/logs", EventsHandlers.GetEventParticipantLogs)
            .WithSummary("Event participant logs")
            .WithDescription("Returns recent entry/exit logs for an event with pagination and filters.")
            .Produces<EventParticipantLogListResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/checkins", EventsHandlers.CheckInByCode)
            .WithSummary("Check-in by code")
            .WithDescription("Marks participant as arrived using checkInCode.")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(op =>
            {
                AddJsonExample(op,
                    new OpenApiObject
                    {
                        ["checkInCode"] = new OpenApiString("QQTL4S88"),
                        ["direction"] = new OpenApiString("Entry"),
                        ["method"] = new OpenApiString("QrScan")
                    }
                );
                return op;
            });

        admin.MapPost("/{eventId}/checkins/undo", EventsHandlers.UndoCheckIn)
            .WithSummary("Undo check-in")
            .WithDescription("Reverts a participant check-in.")
            .Produces<CheckInUndoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/checkins/reset-all", EventsHandlers.ResetAllCheckIns)
            .WithSummary("Reset all check-ins")
            .WithDescription("Removes all check-ins for the event and marks everyone as not arrived.")
            .Produces<ResetAllCheckInsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/participants", EventsHandlers.CreateParticipant)
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

        admin.MapPut("/{eventId}/participants/{participantId}", EventsHandlers.UpdateParticipant)
            .WithSummary("Update participant")
            .WithDescription("Updates participant details.")
            .Produces<ParticipantDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/participants/{participantId}/flights", EventsHandlers.ReplaceParticipantFlights)
            .WithSummary("Replace participant flights")
            .WithDescription("Replaces all arrival/return flight segments for the participant.")
            .Produces<ParticipantFlightsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPatch("/{eventId}/participants/{participantId}/will-not-attend", EventsHandlers.SetParticipantWillNotAttend)
            .WithSummary("Set participant will-not-attend")
            .WithDescription("Marks a participant as will-not-attend (excluded from check-in totals).")
            .Produces<ParticipantWillNotAttendResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/participants/{participantId}", EventsHandlers.DeleteParticipant)
            .WithSummary("Delete participant")
            .WithDescription("Removes a participant from the event.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/participants", EventsHandlers.DeleteAllParticipants)
            .WithSummary("Delete all participants")
            .WithDescription("Removes all participants (and related check-ins/access) from the event.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/checkin", EventsHandlers.CheckIn)
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
                        ["direction"] = new OpenApiString("Entry"),
                        ["method"] = new OpenApiString("manual")
                    }
                );
                return op;
            });

        admin.MapGet("/{eventId}/docs/tabs", EventsHandlers.GetDocTabs)
            .WithSummary("List doc tabs")
            .WithDescription("Returns all document tabs for the event.")
            .Produces<EventDocTabDto[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/{eventId}/docs/tabs", EventsHandlers.CreateDocTab)
            .WithSummary("Create doc tab")
            .WithDescription("Creates a document tab for the event.")
            .Produces<EventDocTabDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/{eventId}/docs/tabs/{tabId}", EventsHandlers.UpdateDocTab)
            .WithSummary("Update doc tab")
            .WithDescription("Updates a document tab for the event.")
            .Produces<EventDocTabDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/{eventId}/docs/tabs/{tabId}", EventsHandlers.DeleteDocTab)
            .WithSummary("Delete doc tab")
            .WithDescription("Deletes a document tab for the event.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/{eventId}/badges.pdf", EventsHandlers.GenerateBadgesPdf)
            .WithSummary("Generate participant badges PDF")
            .WithDescription("Generates a multi-page PDF with participant badges (9x12 cm). Each badge contains event title, date range, participant name, and QR code. Excludes participants with willNotAttend=true.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

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
