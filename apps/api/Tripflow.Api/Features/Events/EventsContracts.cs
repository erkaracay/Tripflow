using System.Text.Json;

namespace Tripflow.Api.Features.Events;

public sealed record CreateEventRequest(string? Name, string? StartDate, string? EndDate, string? EventAccessCode);
public sealed record UpdateEventRequest(string? Name, string? StartDate, string? EndDate);

public sealed record EventDto(Guid Id, string Name, string StartDate, string EndDate, string? LogoUrl, Guid[] GuideUserIds, bool IsDeleted, string EventAccessCode);
public sealed record EventListItemDto(
    Guid Id,
    string Name,
    string StartDate,
    string EndDate,
    int ArrivedCount,
    int TotalCount,
    Guid[] GuideUserIds,
    bool IsDeleted,
    string EventAccessCode,
    string? OrganizationName = null);

public sealed record EventPortalInfo(
    MeetingInfo Meeting,
    LinkInfo[] Links,
    DayPlan[] Days,
    string[] Notes,
    EventContactsDto? EventContacts = null);
public sealed record MeetingInfo(string Time, string Place, string MapsUrl, string Note);
public sealed record LinkInfo(string Label, string Url);
public sealed record DayPlan(int Day, string Title, string[] Items);
public sealed record EventContactsDto(
    string? GuideName,
    string? GuidePhone,
    string? LeaderName,
    string? LeaderPhone,
    string? EmergencyPhone,
    string? WhatsappGroupUrl);
public sealed record UpdateEventContactsRequest(
    string? GuideName,
    string? GuidePhone,
    string? LeaderName,
    string? LeaderPhone,
    string? EmergencyPhone,
    string? WhatsappGroupUrl);

public sealed record EventDayDto(
    Guid Id,
    string Date,
    string? Title,
    string? Notes,
    string? PlacesToVisit,
    int SortOrder,
    bool IsActive,
    int ActivityCount);

public sealed record CreateEventDayRequest(
    string? Date,
    string? Title,
    string? Notes,
    string? PlacesToVisit,
    int? SortOrder,
    bool? IsActive);

public sealed record UpdateEventDayRequest(
    string? Date,
    string? Title,
    string? Notes,
    string? PlacesToVisit,
    int? SortOrder,
    bool? IsActive);

public sealed record EventActivityDto(
    Guid Id,
    Guid EventDayId,
    string Title,
    string Type,
    string? StartTime,
    string? EndTime,
    string? LocationName,
    string? Address,
    string? Directions,
    string? Notes,
    bool CheckInEnabled,
    bool RequiresCheckIn,
    string CheckInMode,
    string? MenuText,
    string? ProgramContent,
    string? SurveyUrl);

public sealed record CreateEventActivityRequest(
    string? Title,
    string? Type,
    string? StartTime,
    string? EndTime,
    string? LocationName,
    string? Address,
    string? Directions,
    string? Notes,
    bool? CheckInEnabled,
    bool? RequiresCheckIn,
    string? CheckInMode,
    string? MenuText,
    string? ProgramContent,
    string? SurveyUrl);

public sealed record UpdateEventActivityRequest(
    string? Title,
    string? Type,
    string? StartTime,
    string? EndTime,
    string? LocationName,
    string? Address,
    string? Directions,
    string? Notes,
    bool? CheckInEnabled,
    bool? RequiresCheckIn,
    string? CheckInMode,
    string? MenuText,
    string? ProgramContent,
    string? SurveyUrl);

public sealed record EventScheduleDayDto(
    Guid Id,
    string Date,
    string? Title,
    string? Notes,
    string? PlacesToVisit,
    int SortOrder,
    bool IsActive,
    EventActivityDto[] Activities);

public sealed record EventScheduleDto(EventScheduleDayDto[] Days);

public sealed record CreateParticipantRequest(
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Phone,
    string? Email,
    string? TcNo,
    string? BirthDate,
    string? Gender);

public sealed record UpdateParticipantRequest(
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Phone,
    string? Email,
    string? TcNo,
    string? BirthDate,
    string? Gender,
    ParticipantDetailsRequest? Details);

public sealed record ParticipantDetailsRequest(
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo,
    string? AgencyName,
    string? City,
    string? FlightCity,
    string? HotelCheckInDate,
    string? HotelCheckOutDate,
    string? TicketNo,
    string? ArrivalTicketNo,
    string? ReturnTicketNo,
    string? AttendanceStatus,
    string? InsuranceCompanyName,
    string? InsurancePolicyNo,
    string? InsuranceStartDate,
    string? InsuranceEndDate,
    string? ArrivalAirline,
    string? ArrivalDepartureAirport,
    string? ArrivalArrivalAirport,
    string? ArrivalFlightCode,
    string? ArrivalFlightDate,
    string? ArrivalDepartureTime,
    string? ArrivalArrivalTime,
    string? ArrivalPnr,
    string? ArrivalBaggageAllowance,
    int? ArrivalBaggagePieces,
    int? ArrivalBaggageTotalKg,
    string? ArrivalCabinBaggage,
    string? ReturnAirline,
    string? ReturnDepartureAirport,
    string? ReturnArrivalAirport,
    string? ReturnFlightCode,
    string? ReturnFlightDate,
    string? ReturnDepartureTime,
    string? ReturnArrivalTime,
    string? ReturnPnr,
    string? ReturnBaggageAllowance,
    int? ReturnBaggagePieces,
    int? ReturnBaggageTotalKg,
    string? ReturnCabinBaggage,
    string? ArrivalTransferPickupTime,
    string? ArrivalTransferPickupPlace,
    string? ArrivalTransferDropoffPlace,
    string? ArrivalTransferVehicle,
    string? ArrivalTransferPlate,
    string? ArrivalTransferDriverInfo,
    string? ArrivalTransferNote,
    string? ReturnTransferPickupTime,
    string? ReturnTransferPickupPlace,
    string? ReturnTransferDropoffPlace,
    string? ReturnTransferVehicle,
    string? ReturnTransferPlate,
    string? ReturnTransferDriverInfo,
    string? ReturnTransferNote);

public sealed record ParticipantDetailsDto(
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo,
    string? AgencyName,
    string? City,
    string? FlightCity,
    string? HotelCheckInDate,
    string? HotelCheckOutDate,
    string? TicketNo,
    string? ArrivalTicketNo,
    string? ReturnTicketNo,
    string? AttendanceStatus,
    string? InsuranceCompanyName,
    string? InsurancePolicyNo,
    string? InsuranceStartDate,
    string? InsuranceEndDate,
    string? ArrivalAirline,
    string? ArrivalDepartureAirport,
    string? ArrivalArrivalAirport,
    string? ArrivalFlightCode,
    string? ArrivalFlightDate,
    string? ArrivalDepartureTime,
    string? ArrivalArrivalTime,
    string? ArrivalPnr,
    string? ArrivalBaggageAllowance,
    int? ArrivalBaggagePieces,
    int? ArrivalBaggageTotalKg,
    string? ArrivalCabinBaggage,
    string? ReturnAirline,
    string? ReturnDepartureAirport,
    string? ReturnArrivalAirport,
    string? ReturnFlightCode,
    string? ReturnFlightDate,
    string? ReturnDepartureTime,
    string? ReturnArrivalTime,
    string? ReturnPnr,
    string? ReturnBaggageAllowance,
    int? ReturnBaggagePieces,
    int? ReturnBaggageTotalKg,
    string? ReturnCabinBaggage,
    string? ArrivalTransferPickupTime,
    string? ArrivalTransferPickupPlace,
    string? ArrivalTransferDropoffPlace,
    string? ArrivalTransferVehicle,
    string? ArrivalTransferPlate,
    string? ArrivalTransferDriverInfo,
    string? ArrivalTransferNote,
    string? ReturnTransferPickupTime,
    string? ReturnTransferPickupPlace,
    string? ReturnTransferDropoffPlace,
    string? ReturnTransferVehicle,
    string? ReturnTransferPlate,
    string? ReturnTransferDriverInfo,
    string? ReturnTransferNote);

public sealed record FlightSegmentDto(
    int SegmentIndex,
    string? Airline,
    string? DepartureAirport,
    string? ArrivalAirport,
    string? FlightCode,
    string? DepartureDate,
    string? DepartureTime,
    string? ArrivalDate,
    string? ArrivalTime,
    string? Pnr,
    string? TicketNo,
    int? BaggagePieces,
    int? BaggageTotalKg,
    string? CabinBaggage);

public sealed record ReplaceParticipantFlightsRequest(
    FlightSegmentDto[]? ArrivalSegments,
    FlightSegmentDto[]? ReturnSegments);

public sealed record ParticipantFlightsResponse(
    FlightSegmentDto[] ArrivalSegments,
    FlightSegmentDto[] ReturnSegments);

public sealed record ParticipantDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string BirthDate,
    string Gender,
    string CheckInCode,
    bool Arrived,
    bool WillNotAttend,
    ParticipantDetailsDto? Details,
    ParticipantLastLogDto? LastLog = null);

public sealed record ParticipantLastLogDto(
    string Direction,
    string Method,
    string Result,
    DateTime CreatedAt);

public sealed record EventDocTabDto(
    Guid Id,
    Guid EventId,
    string Title,
    string Type,
    int SortOrder,
    bool IsActive,
    JsonElement Content);

public sealed record CreateEventDocTabRequest(
    string? Title,
    string? Type,
    int? SortOrder,
    bool? IsActive,
    JsonElement? Content);

public sealed record UpdateEventDocTabRequest(
    string? Title,
    string? Type,
    int? SortOrder,
    bool? IsActive,
    JsonElement? Content);

public sealed record ParticipantWillNotAttendRequest(bool? WillNotAttend);
public sealed record ParticipantWillNotAttendResponseDto(
    Guid Id,
    bool WillNotAttend,
    bool Arrived,
    ParticipantLastLogDto? LastLog);

public sealed record ParticipantProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string BirthDate,
    string Gender,
    string CheckInCode,
    bool Arrived,
    string? ArrivedAt,
    bool TcNoDuplicate,
    ParticipantDetailsDto? Details,
    FlightSegmentDto[] ArrivalSegments,
    FlightSegmentDto[] ReturnSegments);

public sealed record ParticipantTableItemDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string BirthDate,
    string Gender,
    string CheckInCode,
    bool Arrived,
    string? ArrivedAt,
    ParticipantDetailsDto? Details);

public sealed record ParticipantTableResponseDto(
    int Page,
    int PageSize,
    int Total,
    ParticipantTableItemDto[] Items);
public sealed record ParticipantResolveDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    bool Arrived,
    string CheckInCode);

public sealed record CheckInRequest(string? Code, Guid? ParticipantId, string? Method, string? Direction = null);
public sealed record CheckInCodeRequest(string? CheckInCode, string? Code = null, string? Direction = null, string? Method = null);
public sealed record CheckInUndoRequest(Guid? ParticipantId, string? CheckInCode);
public sealed record CheckInSummary(int ArrivedCount, int TotalCount);
public sealed record ResetAllCheckInsResponse(int RemovedCount, int ArrivedCount, int TotalCount);
public sealed record CheckInResponse(
    Guid ParticipantId,
    string ParticipantName,
    bool AlreadyArrived,
    int ArrivedCount,
    int TotalCount,
    string? Direction = null,
    DateTime? LoggedAt = null,
    string? Result = null);
public sealed record CheckInUndoResponse(Guid ParticipantId, bool AlreadyUndone, int ArrivedCount, int TotalCount);

public sealed record EventParticipantLogItemDto(
    Guid Id,
    string CreatedAt,
    string Direction,
    string Method,
    string Result,
    Guid? ParticipantId,
    string? ParticipantName,
    string? ParticipantTcNo,
    string? ParticipantPhone,
    string? CheckInCode,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorRole,
    string? IpAddress = null,
    string? UserAgent = null);

public sealed record EventParticipantLogListResponseDto(
    int Page,
    int PageSize,
    int Total,
    EventParticipantLogItemDto[] Items);

public sealed record VerifyCheckInCodeRequest(string? CheckInCode);
public sealed record VerifyCheckInCodeResponse(bool IsValid, string? NormalizedCode);
public sealed record AssignGuidesRequest(Guid[] GuideUserIds);
public sealed record EventAccessCodeResponse(Guid EventId, string EventAccessCode);
public sealed record UpdateEventAccessCodeRequest(string? EventAccessCode);
