namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantAccommodationStayEntity
{
    public Guid Id { get; set; }

    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;

    public Guid EventAccommodationId { get; set; }
    public EventDocTabEntity EventAccommodation { get; set; } = default!;

    public Guid OrganizationId { get; set; }
    public Guid EventId { get; set; }

    public string? RoomNo { get; set; }
    public string? RoomType { get; set; }
    public string? BoardType { get; set; }
    public string? PersonNo { get; set; }

    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
