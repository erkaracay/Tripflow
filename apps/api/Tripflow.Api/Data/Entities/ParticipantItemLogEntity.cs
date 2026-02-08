namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantItemLogEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public Guid? ParticipantId { get; set; }

    public string Action { get; set; } = "Give";   // Give | Return
    public string Method { get; set; } = "Manual";  // Manual | QrScan
    public string Result { get; set; } = "Success"; // Success | NotFound | InvalidRequest | Failed

    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
