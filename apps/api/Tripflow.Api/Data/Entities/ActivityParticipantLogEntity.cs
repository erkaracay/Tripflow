namespace Tripflow.Api.Data.Entities;

public sealed class ActivityParticipantLogEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EventId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? ParticipantId { get; set; }

    public string Direction { get; set; } = "Entry"; // Entry | Exit
    public string Method { get; set; } = "Manual";   // Manual | QrScan
    public string Result { get; set; } = "Success";   // Success | AlreadyCheckedIn | NotFound | InvalidRequest | Failed

    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
