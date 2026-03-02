namespace Tripflow.Api.Data.Entities;

public sealed class ActivityMealGroupEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public Guid ActivityId { get; set; }
    public EventActivityEntity Activity { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool AllowOther { get; set; }
    public bool AllowNote { get; set; }
    public bool IsActive { get; set; }

    public List<ActivityMealOptionEntity> Options { get; set; } = new();
    public List<ParticipantMealSelectionEntity> Selections { get; set; } = new();
}
