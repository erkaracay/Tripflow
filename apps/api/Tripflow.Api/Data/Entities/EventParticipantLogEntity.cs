namespace Tripflow.Api.Data.Entities;

public enum EventParticipantLogDirection
{
    Entry = 0,
    Exit = 1
}

public enum EventParticipantLogMethod
{
    Manual = 0,
    QrScan = 1
}

public sealed class EventParticipantLogEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public Guid EventId { get; set; }
    public Guid? ParticipantId { get; set; }

    public EventParticipantLogDirection Direction { get; set; }
    public EventParticipantLogMethod Method { get; set; }

    public string Result { get; set; } = "Success";

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }

    public DateTime CreatedAt { get; set; }
}
