namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantAccommodationAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EventId { get; set; }
    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;
    public Guid SegmentId { get; set; }
    public EventAccommodationSegmentEntity Segment { get; set; } = default!;
    public Guid? OverrideAccommodationDocTabId { get; set; }
    public EventDocTabEntity? OverrideAccommodationDocTab { get; set; }
    public string? RoomNo { get; set; }
    public string? RoomType { get; set; }
    public string? BoardType { get; set; }
    public string? PersonNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
