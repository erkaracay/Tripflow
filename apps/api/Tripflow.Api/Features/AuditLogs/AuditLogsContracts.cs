namespace Tripflow.Api.Features.AuditLogs;

public sealed record AuditLogListItemDto(
    long Id,
    DateTime CreatedAt,
    Guid? UserId,
    string? UserEmail,
    string? UserFullName,
    string? Role,
    Guid? OrganizationId,
    string Action,
    string TargetType,
    string? TargetId,
    string? IpAddress,
    string Result,
    string? ExtraJson);

public sealed record AuditLogListResponse(
    AuditLogListItemDto[] Items,
    int Total,
    int Page,
    int PageSize);
