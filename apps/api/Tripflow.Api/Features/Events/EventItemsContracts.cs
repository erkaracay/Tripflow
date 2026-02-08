namespace Tripflow.Api.Features.Events;

public sealed record EventItemDto(Guid Id, string Type, string Title, string Name, bool IsActive, int SortOrder);
public sealed record CreateEventItemRequest(string Type, string Title, string Name, int? SortOrder);
public sealed record UpdateEventItemRequest(string? Type, string? Title, string? Name, bool? IsActive, int? SortOrder);
public sealed record ItemActionRequest(string? CheckInCode, string? Code = null, string? Action = null, string? Method = null);
public sealed record ItemActionResponse(
    Guid ParticipantId,
    string ParticipantName,
    string Result,
    string Action,
    string Method,
    DateTime LoggedAt);

public sealed record ItemParticipantTableItemDto(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string CheckInCode,
    string? RoomNo,
    string? AgencyName,
    ItemParticipantStateDto ItemState);

public sealed record ItemParticipantStateDto(bool Given, ItemLastLogDto? LastLog); // Given = not returned
public sealed record ItemLastLogDto(string Action, string Method, string Result, string CreatedAt);

public sealed record ItemParticipantTableResponseDto(
    int Page,
    int PageSize,
    int Total,
    ItemParticipantTableItemDto[] Items);
