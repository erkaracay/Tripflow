namespace Tripflow.Api.Data.Entities;

public sealed class OrganizationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<UserEntity> Users { get; set; } = new();
    public List<EventEntity> Events { get; set; } = new();
    public List<ParticipantEntity> Participants { get; set; } = new();
    public List<EventDayEntity> EventDays { get; set; } = new();
    public List<EventActivityEntity> EventActivities { get; set; } = new();
    public List<PortalSessionEntity> PortalSessions { get; set; } = new();
    public List<CheckInEntity> CheckIns { get; set; } = new();
    public List<EventPortalEntity> Portals { get; set; } = new();
    public List<EventDocTabEntity> DocTabs { get; set; } = new();
}
