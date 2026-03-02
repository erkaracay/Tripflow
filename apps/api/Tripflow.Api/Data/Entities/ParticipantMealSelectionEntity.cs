namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantMealSelectionEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public Guid ActivityId { get; set; }
    public EventActivityEntity Activity { get; set; } = default!;
    public Guid GroupId { get; set; }
    public ActivityMealGroupEntity Group { get; set; } = default!;
    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;
    public Guid? OptionId { get; set; }
    public ActivityMealOptionEntity? Option { get; set; }
    public string? OtherText { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
