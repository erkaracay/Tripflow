using Tripflow.Api.Data;
using Tripflow.Api.Data.Dev;
using Tripflow.Api.Features.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Tripflow.Api.Features.Dev;

internal static class DevToolsHandlers
{
    internal static IResult GetTools()
        => Results.Ok(new DevToolsCapabilitiesResponse(
            GeneralSeed: true,
            ScenarioEventGenerator: true,
            Presets: ScenarioEventGenerator.GetPresetDtos()));

    internal static async Task<IResult> CreateScenarioEvent(
        CreateScenarioEventRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var organizationId, out var orgError))
        {
            return orgError!;
        }

        if (!ScenarioEventGenerator.TryResolveRequest(request, out var resolved, out var validationError))
        {
            return Results.BadRequest(new
            {
                code = validationError!.Code,
                field = validationError.Field,
                message = validationError.Message
            });
        }

        var response = await ScenarioEventGenerator.GenerateAsync(db, organizationId, resolved!, ct);
        return Results.Created($"/api/events/{response.EventId}", response);
    }

    internal static async Task<IResult> DeleteScenarioEvent(
        string eventId,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var organizationId, out var orgError))
        {
            return orgError!;
        }

        if (!Guid.TryParse(eventId, out var eventGuid))
        {
            return Results.BadRequest(new { code = "invalid_event_id", message = "eventId must be a valid UUID." });
        }

        var entity = await db.Events
            .FirstOrDefaultAsync(x => x.Id == eventGuid && x.OrganizationId == organizationId, ct);

        if (entity is null)
        {
            return Results.NotFound(new { code = "event_not_found", message = "Event not found." });
        }

        if (!entity.Name.StartsWith("[DEV]", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { code = "not_generated_scenario_event", message = "Only generated development scenario events can be deleted from this tool." });
        }

        db.Events.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new DeleteScenarioEventResponse(entity.Id, true));
    }
}
