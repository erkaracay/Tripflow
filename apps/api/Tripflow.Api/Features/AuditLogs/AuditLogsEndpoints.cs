using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tripflow.Api.Features.AuditLogs;

public static class AuditLogsEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogs(this IEndpointRouteBuilder app)
    {
        app.MapGet("", AuditLogsHandlers.GetAuditLogs)
            .WithSummary("List audit logs")
            .WithDescription("Returns a paginated audit log list for admin users.")
            .Produces<AuditLogListResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
