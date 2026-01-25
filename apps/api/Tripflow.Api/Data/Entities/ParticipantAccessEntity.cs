namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantAccessEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;

    public int Version { get; set; }
    public string Secret { get; set; } = default!;
    public string SecretHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
