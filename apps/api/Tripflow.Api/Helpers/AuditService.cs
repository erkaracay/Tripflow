using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Helpers;

internal sealed record AuditLogWrite(
    string Action,
    string TargetType,
    string? TargetId,
    string Result,
    Guid? OrganizationId = null,
    Guid? UserId = null,
    string? Role = null,
    IReadOnlyDictionary<string, object?>? Extra = null);

internal sealed class AuditService(
    IDbContextFactory<TripflowDbContext> dbContextFactory,
    ILogger<AuditService> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task LogAsync(HttpContext httpContext, AuditLogWrite entry, CancellationToken ct)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(ct);

            var userId = entry.UserId;
            if (!userId.HasValue && AuditLogHelpers.TryResolveUserId(httpContext.User, out var resolvedUserId))
            {
                userId = resolvedUserId;
            }

            var role = string.IsNullOrWhiteSpace(entry.Role)
                ? AuditLogHelpers.ResolveRole(httpContext.User)
                : entry.Role!.Trim();

            var organizationId = entry.OrganizationId;
            if (!organizationId.HasValue && AuditLogHelpers.TryResolveOrganizationId(httpContext, out var resolvedOrgId))
            {
                organizationId = resolvedOrgId;
            }

            var extra = entry.Extra is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(entry.Extra, StringComparer.Ordinal);

            if (!extra.ContainsKey("correlationId"))
            {
                extra["correlationId"] = httpContext.TraceIdentifier;
            }

            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrWhiteSpace(userAgent) && !extra.ContainsKey("userAgent"))
            {
                extra["userAgent"] = userAgent;
            }

            db.Set<AuditLogEntity>().Add(new AuditLogEntity
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                Role = string.IsNullOrWhiteSpace(role) ? "Anonymous" : role,
                OrganizationId = organizationId,
                Action = entry.Action,
                TargetType = entry.TargetType,
                TargetId = entry.TargetId,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                Result = entry.Result,
                ExtraJson = extra.Count == 0 ? null : JsonSerializer.Serialize(extra, SerializerOptions)
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Audit log write failed for {Action} {TargetType} {TargetId}",
                entry.Action,
                entry.TargetType,
                entry.TargetId);
        }
    }
}
