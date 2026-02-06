namespace Tripflow.Api.Data.Entities;

public sealed class EventDocTabEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Type { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string ContentJson { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
