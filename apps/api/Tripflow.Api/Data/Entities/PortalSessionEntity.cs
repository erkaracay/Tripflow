namespace Tripflow.Api.Data.Entities;

public sealed class PortalSessionEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;

    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;

    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
