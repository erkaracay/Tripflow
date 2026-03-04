namespace Tripflow.Api.Data.Entities;

public sealed class EventItemEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public string Type { get; set; } = "Other";   // e.g. Headset
    public string Title { get; set; } = "Equipment";
    public string Name { get; set; } = default!;   // e.g. "Headset / Kulaklık"
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 1;
}
