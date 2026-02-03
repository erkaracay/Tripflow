namespace Tripflow.Api.Data.Entities;

public enum ParticipantGender
{
    Female = 0,
    Male = 1,
    Other = 2
}

public sealed class ParticipantEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string TcNo { get; set; } = default!;
    public DateOnly BirthDate { get; set; }
    public ParticipantGender Gender { get; set; }

    // QR / check-in için tekil kod (GUID string de olabilir)
    public string CheckInCode { get; set; } = default!;

    public ParticipantDetailsEntity? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
