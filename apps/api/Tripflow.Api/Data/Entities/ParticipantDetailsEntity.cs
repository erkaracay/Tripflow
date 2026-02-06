namespace Tripflow.Api.Data.Entities;

public sealed class ParticipantDetailsEntity
{
    public Guid ParticipantId { get; set; }
    public ParticipantEntity Participant { get; set; } = default!;

    public string? RoomNo { get; set; }
    public string? RoomType { get; set; }
    public string? BoardType { get; set; }
    public string? PersonNo { get; set; }
    public string? AgencyName { get; set; }
    public string? City { get; set; }
    public string? FlightCity { get; set; }

    public DateOnly? HotelCheckInDate { get; set; }
    public DateOnly? HotelCheckOutDate { get; set; }

    public string? TicketNo { get; set; }
    public string? AttendanceStatus { get; set; }

    public string? InsuranceCompanyName { get; set; }
    public string? InsurancePolicyNo { get; set; }
    public DateOnly? InsuranceStartDate { get; set; }
    public DateOnly? InsuranceEndDate { get; set; }

    public string? ArrivalAirline { get; set; }
    public string? ArrivalDepartureAirport { get; set; }
    public string? ArrivalArrivalAirport { get; set; }
    public string? ArrivalFlightCode { get; set; }
    public TimeOnly? ArrivalDepartureTime { get; set; }
    public TimeOnly? ArrivalArrivalTime { get; set; }
    public string? ArrivalPnr { get; set; }
    public string? ArrivalBaggageAllowance { get; set; }
    public int? ArrivalBaggagePieces { get; set; }
    public int? ArrivalBaggageTotalKg { get; set; }

    public string? ReturnAirline { get; set; }
    public string? ReturnDepartureAirport { get; set; }
    public string? ReturnArrivalAirport { get; set; }
    public string? ReturnFlightCode { get; set; }
    public TimeOnly? ReturnDepartureTime { get; set; }
    public TimeOnly? ReturnArrivalTime { get; set; }
    public string? ReturnPnr { get; set; }
    public string? ReturnBaggageAllowance { get; set; }
    public int? ReturnBaggagePieces { get; set; }
    public int? ReturnBaggageTotalKg { get; set; }
}
