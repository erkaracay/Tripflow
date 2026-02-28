namespace Tripflow.Api.Data.Entities;

public enum ParticipantFlightSegmentDirection
{
    Arrival = 0,
    Return = 1
}

public sealed class ParticipantFlightSegmentEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;

    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;

    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;

    public ParticipantFlightSegmentDirection Direction { get; set; }
    public int SegmentIndex { get; set; }

    public string? Airline { get; set; }
    public string? DepartureAirport { get; set; }
    public string? ArrivalAirport { get; set; }
    public string? FlightCode { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public TimeOnly? DepartureTime { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public TimeOnly? ArrivalTime { get; set; }
    public string? Pnr { get; set; }
    public string? TicketNo { get; set; }
    public int? BaggagePieces { get; set; }
    public int? BaggageTotalKg { get; set; }
    public string? CabinBaggage { get; set; }
}
