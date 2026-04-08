namespace Tripflow.Api.Data.Entities;

public sealed class EventAccommodationSegmentEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public Guid DefaultAccommodationDocTabId { get; set; }
    public EventDocTabEntity DefaultAccommodationDocTab { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ParticipantAccommodationAssignmentEntity> ParticipantAssignments { get; set; } = new();
}
