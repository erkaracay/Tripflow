namespace Tripflow.Api.Features.Events;

public sealed record CreateEventRequest(string? Name, string? StartDate, string? EndDate);
public sealed record UpdateEventRequest(string? Name, string? StartDate, string? EndDate);

public sealed record EventDto(Guid Id, string Name, string StartDate, string EndDate, Guid? GuideUserId, bool IsDeleted, string EventAccessCode);
public sealed record EventListItemDto(
    Guid Id,
    string Name,
    string StartDate,
    string EndDate,
    int ArrivedCount,
    int TotalCount,
    Guid? GuideUserId,
    bool IsDeleted,
    string EventAccessCode);

public sealed record EventPortalInfo(MeetingInfo Meeting, LinkInfo[] Links, DayPlan[] Days, string[] Notes);
public sealed record MeetingInfo(string Time, string Place, string MapsUrl, string Note);
public sealed record LinkInfo(string Label, string Url);
public sealed record DayPlan(int Day, string Title, string[] Items);

public sealed record EventDayDto(
    Guid Id,
    string Date,
    string? Title,
    string? Notes,
    int SortOrder,
    bool IsActive,
    int ActivityCount);

public sealed record CreateEventDayRequest(
    string? Date,
    string? Title,
    string? Notes,
    int? SortOrder,
    bool? IsActive);

public sealed record UpdateEventDayRequest(
    string? Date,
    string? Title,
    string? Notes,
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
    string CheckInMode,
    string? MenuText,
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
    string? CheckInMode,
    string? MenuText,
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
    string? CheckInMode,
    string? MenuText,
    string? SurveyUrl);

public sealed record EventScheduleDayDto(
    Guid Id,
    string Date,
    string? Title,
    string? Notes,
    int SortOrder,
    bool IsActive,
    EventActivityDto[] Activities);

public sealed record EventScheduleDto(EventScheduleDayDto[] Days);

public sealed record CreateParticipantRequest(
    string? FullName,
    string? Phone,
    string? Email,
    string? TcNo,
    string? BirthDate,
    string? Gender);

public sealed record UpdateParticipantRequest(
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
    string? PersonNo,
    string? AgencyName,
    string? City,
    string? FlightCity,
    string? HotelCheckInDate,
    string? HotelCheckOutDate,
    string? TicketNo,
    string? AttendanceStatus,
    string? ArrivalAirline,
    string? ArrivalDepartureAirport,
    string? ArrivalArrivalAirport,
    string? ArrivalFlightCode,
    string? ArrivalDepartureTime,
    string? ArrivalArrivalTime,
    string? ArrivalPnr,
    string? ArrivalBaggageAllowance,
    int? ArrivalBaggagePieces,
    int? ArrivalBaggageTotalKg,
    string? ReturnAirline,
    string? ReturnDepartureAirport,
    string? ReturnArrivalAirport,
    string? ReturnFlightCode,
    string? ReturnDepartureTime,
    string? ReturnArrivalTime,
    string? ReturnPnr,
    string? ReturnBaggageAllowance,
    int? ReturnBaggagePieces,
    int? ReturnBaggageTotalKg);

public sealed record ParticipantDetailsDto(
    string? RoomNo,
    string? RoomType,
    string? PersonNo,
    string? AgencyName,
    string? City,
    string? FlightCity,
    string? HotelCheckInDate,
    string? HotelCheckOutDate,
    string? TicketNo,
    string? AttendanceStatus,
    string? ArrivalAirline,
    string? ArrivalDepartureAirport,
    string? ArrivalArrivalAirport,
    string? ArrivalFlightCode,
    string? ArrivalDepartureTime,
    string? ArrivalArrivalTime,
    string? ArrivalPnr,
    string? ArrivalBaggageAllowance,
    int? ArrivalBaggagePieces,
    int? ArrivalBaggageTotalKg,
    string? ReturnAirline,
    string? ReturnDepartureAirport,
    string? ReturnArrivalAirport,
    string? ReturnFlightCode,
    string? ReturnDepartureTime,
    string? ReturnArrivalTime,
    string? ReturnPnr,
    string? ReturnBaggageAllowance,
    int? ReturnBaggagePieces,
    int? ReturnBaggageTotalKg);

public sealed record ParticipantDto(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string BirthDate,
    string Gender,
    string CheckInCode,
    bool Arrived,
    ParticipantDetailsDto? Details);

public sealed record ParticipantProfileDto(
    Guid Id,
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
    ParticipantDetailsDto? Details);
public sealed record ParticipantResolveDto(Guid Id, string FullName, bool Arrived, string CheckInCode);

public sealed record CheckInRequest(string? Code, Guid? ParticipantId, string? Method);
public sealed record CheckInCodeRequest(string? CheckInCode);
public sealed record CheckInUndoRequest(Guid? ParticipantId, string? CheckInCode);
public sealed record CheckInSummary(int ArrivedCount, int TotalCount);
public sealed record ResetAllCheckInsResponse(int RemovedCount, int ArrivedCount, int TotalCount);
public sealed record CheckInResponse(Guid ParticipantId, string ParticipantName, bool AlreadyArrived, int ArrivedCount, int TotalCount);
public sealed record CheckInUndoResponse(Guid ParticipantId, bool AlreadyUndone, int ArrivedCount, int TotalCount);
public sealed record VerifyCheckInCodeRequest(string? CheckInCode);
public sealed record VerifyCheckInCodeResponse(bool IsValid, string? NormalizedCode);
public sealed record AssignGuideRequest(Guid? GuideUserId);
public sealed record EventAccessCodeResponse(Guid EventId, string EventAccessCode);
