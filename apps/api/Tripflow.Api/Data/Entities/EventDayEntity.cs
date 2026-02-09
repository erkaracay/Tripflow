namespace Tripflow.Api.Data.Entities;

public sealed class EventDayEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public DateOnly Date { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public string? PlacesToVisit { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EventActivityEntity> Activities { get; set; } = new();
}
