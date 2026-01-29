namespace Tripflow.Api.Data.Entities;

public sealed class CheckInEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public DateTime CheckedInAt { get; set; }
    public string Method { get; set; } = "manual"; // "qr" | "manual"
}
