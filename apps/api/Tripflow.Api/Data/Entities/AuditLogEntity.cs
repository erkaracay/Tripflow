namespace Tripflow.Api.Data.Entities;

public sealed class AuditLogEntity
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public Guid? OrganizationId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? IpAddress { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ExtraJson { get; set; }
}
