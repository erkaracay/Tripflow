using System.Text.Json;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

public sealed record PortalLoginRequest(string? EventAccessCode, string? TcNo);

public sealed record PortalLoginResponse(
    string PortalSessionToken,
    DateTime ExpiresAt,
    Guid EventId,
    Guid ParticipantId);

public sealed record PortalResolveEventResponse(Guid EventId, string EventTitle);

public sealed record PortalMeResponse(
    PortalEventSummary Event,
    PortalParticipantSummaryFull Participant,
    EventPortalInfo Portal,
    EventScheduleDto Schedule,
    PortalDocsResponse Docs);

public sealed record PortalEventSummary(
    Guid Id,
    string Name,
    string StartDate,
    string EndDate,
    string? LogoUrl);

public sealed record PortalParticipantSummaryFull(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string BirthDate,
    string Gender,
    string CheckInCode);

public sealed record PortalDocsResponse(
    PortalDocTabDto[] Tabs,
    PortalParticipantTravel ParticipantTravel);

public sealed record PortalDocTabDto(
    Guid Id,
    string Title,
    string Type,
    int SortOrder,
    JsonElement Content);

public sealed record PortalParticipantTravel(
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? HotelCheckInDate,
    string? HotelCheckOutDate,
    string? TicketNo,
    string? ArrivalBaggageAllowance,
    string? ReturnBaggageAllowance,
    PortalFlightInfo? Arrival,
    PortalFlightInfo? Return,
    PortalTransferInfo? TransferOutbound,
    PortalTransferInfo? TransferReturn,
    PortalInsuranceInfo? Insurance);

public sealed record PortalFlightInfo(
    string? Airline,
    string? DepartureAirport,
    string? ArrivalAirport,
    string? FlightCode,
    string? TicketNo,
    string? DepartureTime,
    string? ArrivalTime,
    string? Pnr,
    int? BaggagePieces,
    int? BaggageTotalKg,
    string? CabinBaggage);

public sealed record PortalInsuranceInfo(
    string? CompanyName,
    string? PolicyNo,
    string? StartDate,
    string? EndDate);

public sealed record PortalTransferInfo(
    string? PickupTime,
    string? PickupPlace,
    string? DropoffPlace,
    string? Vehicle,
    string? Plate,
    string? DriverInfo,
    string? Note);
