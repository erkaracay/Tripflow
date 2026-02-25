namespace Tripflow.Api.Data.Entities;

public sealed class EventEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? LogoUrl { get; set; }
    public string? GuideName { get; set; }
    public string? GuidePhone { get; set; }
    public string? LeaderName { get; set; }
    public string? LeaderPhone { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? WhatsappGroupUrl { get; set; }
    public string EventAccessCode { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<EventGuideEntity> EventGuides { get; set; } = new();
    public List<ParticipantEntity> Participants { get; set; } = new();
    public List<ParticipantFlightSegmentEntity> ParticipantFlightSegments { get; set; } = new();
    public EventPortalEntity? Portal { get; set; }
    public List<EventDayEntity> Days { get; set; } = new();
    public List<EventDocTabEntity> DocTabs { get; set; } = new();
}
