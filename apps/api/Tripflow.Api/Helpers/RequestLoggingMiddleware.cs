using System.Diagnostics;

namespace Tripflow.Api.Helpers;

internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = AuditLogHelpers.EnsureCorrelationId(context);
        if (AuditLogHelpers.ShouldSkipRequestLogging(context.Request.Path))
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            startedAt.Stop();
            logger.LogError(
                ex,
                "HTTP request failed {correlationId} {method} {path} {durationMs} {remoteIp} {userId} {role} {orgId}",
                correlationId,
                context.Request.Method,
                context.Request.Path.Value,
                startedAt.ElapsedMilliseconds,
                context.Connection.RemoteIpAddress?.ToString(),
                AuditLogHelpers.TryResolveUserId(context.User, out var userId) ? userId : null,
                AuditLogHelpers.ResolveRole(context.User),
                AuditLogHelpers.TryResolveOrganizationId(context, out var orgId) ? orgId : null);
            throw;
        }

        startedAt.Stop();
        logger.LogInformation(
            "HTTP request completed {correlationId} {method} {path} {statusCode} {durationMs} {remoteIp} {userId} {role} {orgId}",
            correlationId,
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            startedAt.ElapsedMilliseconds,
            context.Connection.RemoteIpAddress?.ToString(),
            AuditLogHelpers.TryResolveUserId(context.User, out var completedUserId) ? completedUserId : null,
            AuditLogHelpers.ResolveRole(context.User),
            AuditLogHelpers.TryResolveOrganizationId(context, out var completedOrgId) ? completedOrgId : null);
    }
}
