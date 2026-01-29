namespace Tripflow.Api.Data.Entities;

public sealed class EventPortalEntity
{
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public string PortalJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
