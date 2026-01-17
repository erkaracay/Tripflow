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

    public DateTime CreatedAt { get; set; }
}
