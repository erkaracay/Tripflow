using System.Text.Json;

namespace Tripflow.Api.Features.Events;

public sealed record CreateEventRequest(string? Name, string? StartDate, string? EndDate, string? TimeZoneId, string? EventAccessCode);
public sealed record UpdateEventRequest(string? Name, string? StartDate, string? EndDate, string? TimeZoneId);

public sealed record EventDto(Guid Id, string Name, string StartDate, string EndDate, string? TimeZoneId, string? LogoUrl, Guid[] GuideUserIds, bool IsDeleted, string EventAccessCode);
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
    string? ArrivalTransferSeatNo,
    string? ArrivalTransferCompartmentNo,
    string? ReturnTransferPickupTime,
    string? ReturnTransferPickupPlace,
    string? ReturnTransferDropoffPlace,
    string? ReturnTransferVehicle,
    string? ReturnTransferPlate,
    string? ReturnTransferDriverInfo,
    string? ReturnTransferNote,
    string? ReturnTransferSeatNo,
    string? ReturnTransferCompartmentNo,
    Guid? AccommodationDocTabId = null);

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
    string? ArrivalTransferSeatNo,
    string? ArrivalTransferCompartmentNo,
    string? ReturnTransferPickupTime,
    string? ReturnTransferPickupPlace,
    string? ReturnTransferDropoffPlace,
    string? ReturnTransferVehicle,
    string? ReturnTransferPlate,
    string? ReturnTransferDriverInfo,
    string? ReturnTransferNote,
    string? ReturnTransferSeatNo,
    string? ReturnTransferCompartmentNo,
    Guid? AccommodationDocTabId = null);

public sealed record ParticipantRoomFiltersRequest(
    string? Query,
    string? Status,
    string? AccommodationFilter);

public sealed record ParticipantRoomPatchRequest(
    Guid? AccommodationDocTabId,
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo,
    string? HotelCheckInDate,
    string? HotelCheckOutDate);

public sealed record ParticipantRoomRowUpdateRequest(
    Guid ParticipantId,
    string? TcNo,
    ParticipantRoomPatchRequest? Patch);

public sealed record BulkApplyParticipantRoomsRequest(
    string? Scope,
    Guid[]? ParticipantIds,
    ParticipantRoomFiltersRequest? Filters,
    ParticipantRoomPatchRequest? Patch,
    string? OverwriteMode,
    ParticipantRoomRowUpdateRequest[]? RowUpdates);

public sealed record BulkApplyParticipantRoomsErrorDto(
    Guid? ParticipantId,
    string? TcNo,
    string Code,
    string Message);

public sealed record BulkApplyParticipantRoomsResponse(
    int AffectedCount,
    int UpdatedCount,
    int SkippedCount,
    int NotFoundTcNoCount,
    BulkApplyParticipantRoomsErrorDto[] Errors);

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

public sealed record BulkApplyFlightSegmentsSegmentsDto(
    FlightSegmentDto[]? Arrival,
    FlightSegmentDto[]? Return);

public sealed record BulkApplyFlightSegmentsRequest(
    Guid[]? ParticipantIds,
    string[]? ApplyDirections,
    BulkApplyFlightSegmentsSegmentsDto? Segments,
    string? ReplaceMode);

public sealed record BulkApplyFlightSegmentsAppliedDto(
    int? Arrival,
    int? Return);

public sealed record BulkApplyFlightSegmentsResponse(
    int AffectedCount,
    BulkApplyFlightSegmentsAppliedDto Applied);

public sealed record BulkMatchFlightTicketEntry(
    string? TcNo,
    string? TicketNo);

public sealed record BulkMatchFlightTicketRequest(
    string? Direction,
    string? OverwriteMode,
    BulkMatchFlightTicketEntry[]? Entries);

public sealed record BulkMatchFlightTicketResponse(
    int AppliedParticipantCount,
    int AppliedSegmentCount,
    string[] UnmatchedTcNos,
    string[] NoSegmentsTcNos);

public sealed record ApplyTicketToMatchingFlightsRequest(
    string? Airline,
    string? TicketNo);

public sealed record ApplyTicketToMatchingFlightsResponse(
    int AffectedCount);

public sealed record BulkApplyCommonInsuranceRequest(
    string? CompanyName,
    string? StartDate,
    string? EndDate,
    string? Scope,
    string? OverwriteMode);

public sealed record BulkApplyCommonInsuranceResponse(
    int AffectedCount,
    int SkippedCount);

public sealed record BulkMatchInsurancePolicyEntry(
    string? TcNo,
    string? PolicyNo);

public sealed record BulkMatchInsurancePolicyRequest(
    BulkMatchInsurancePolicyEntry[]? Entries);

public sealed record BulkMatchInsurancePolicyResponse(
    int AppliedCount,
    string[] UnmatchedTcNos);

public sealed record BulkTransferCommonLeg(
    string? PickupTime,
    string? PickupPlace,
    string? DropoffPlace,
    string? Vehicle,
    string? Plate,
    string? DriverInfo,
    string? Note);

public sealed record BulkApplyCommonTransferRequest(
    BulkTransferCommonLeg? Arrival,
    BulkTransferCommonLeg? Return,
    string? Scope,
    string? OverwriteMode);

public sealed record BulkApplyCommonTransferResponse(
    int AffectedCount,
    int SkippedCount);

public sealed record BulkMatchTransferSeatsEntry(
    string? TcNo,
    string? ArrivalSeatNo,
    string? ArrivalCompartmentNo,
    string? ReturnSeatNo,
    string? ReturnCompartmentNo);

public sealed record BulkMatchTransferSeatsRequest(
    BulkMatchTransferSeatsEntry[]? Entries);

public sealed record BulkMatchTransferSeatsResponse(
    int AppliedCount,
    string[] UnmatchedTcNos);

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

public sealed record DocTabInUseSegmentDto(
    Guid Id,
    string StartDate,
    string EndDate,
    int SortOrder,
    int ParticipantCount);

public sealed record DocTabInUseResponse(
    string Code,
    string Message,
    DocTabInUseSegmentDto[] Segments);

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
    bool HasArrivalSegments,
    bool HasReturnSegments,
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
