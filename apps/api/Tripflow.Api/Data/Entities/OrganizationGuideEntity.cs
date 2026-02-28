namespace Tripflow.Api.Data.Entities;

public sealed class OrganizationGuideEntity
{
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public Guid GuideUserId { get; set; }
    public UserEntity GuideUser { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
}
