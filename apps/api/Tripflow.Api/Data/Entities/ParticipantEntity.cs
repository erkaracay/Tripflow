namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantEntity
{
    public Guid Id { get; set; }

    public Guid TourId { get; set; }
    public TourEntity Tour { get; set; } = default!;
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // QR / check-in için tekil kod (GUID string de olabilir)
    public string CheckInCode { get; set; } = default!;

    public int PortalFailedAttempts { get; set; }
    public DateTime? PortalLockedUntil { get; set; }
    public DateTime? PortalLastFailedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
