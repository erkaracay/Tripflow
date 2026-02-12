namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantActivityWillNotAttendEntity
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;
    public Guid ActivityId { get; set; }
    public EventActivityEntity Activity { get; set; } = default!;
    public bool WillNotAttend { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
