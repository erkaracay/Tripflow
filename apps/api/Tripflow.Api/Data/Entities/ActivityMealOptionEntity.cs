namespace Tripflow.Api.Data.Entities;

public sealed class ActivityMealOptionEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid GroupId { get; set; }
    public ActivityMealGroupEntity Group { get; set; } = default!;
    public string Label { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public List<ParticipantMealSelectionEntity> Selections { get; set; } = new();
}
