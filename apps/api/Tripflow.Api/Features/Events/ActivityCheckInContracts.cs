namespace Tripflow.Api.Features.Events;

public sealed record ActivityCheckInRequest(string? CheckInCode, string? Code = null, string? Direction = null, string? Method = null);
public sealed record ActivityCheckInResponse(
    Guid ParticipantId,
    string ParticipantName,
    string Result, // Success | AlreadyCheckedIn | NotFound | InvalidRequest | Failed
    string Direction,
    string Method,
    DateTime LoggedAt);

public sealed record ActivityParticipantTableItemDto(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string CheckInCode,
    string? RoomNo,
    string? AgencyName,
    ActivityParticipantStateDto ActivityState);

public sealed record ActivityParticipantStateDto(bool IsCheckedIn, ActivityLastLogDto? LastLog);
public sealed record ActivityLastLogDto(string Direction, string Method, string Result, string CreatedAt); // HH:mm

public sealed record ActivityParticipantTableResponseDto(
    int Page,
    int PageSize,
    int Total,
    ActivityParticipantTableItemDto[] Items);
