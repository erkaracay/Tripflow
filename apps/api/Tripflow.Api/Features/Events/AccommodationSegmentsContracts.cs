namespace Tripflow.Api.Features.Events;

public sealed record UpsertAccommodationSegmentRequest(
    Guid? DefaultAccommodationDocTabId,
    string? StartDate,
    string? EndDate,
    int? SortOrder);

public sealed record AccommodationSegmentDto(
    Guid Id,
    Guid DefaultAccommodationDocTabId,
    string DefaultAccommodationTitle,
    string StartDate,
    string EndDate,
    int SortOrder);

public sealed record AccommodationSegmentParticipantTableItemDto(
    Guid ParticipantId,
    string FullName,
    string TcNo,
    Guid EffectiveAccommodationDocTabId,
    string EffectiveAccommodationTitle,
    bool UsesOverride,
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo);

public sealed record AccommodationSegmentParticipantTableResponseDto(
    int Page,
    int PageSize,
    int Total,
    AccommodationSegmentParticipantTableItemDto[] Items);

public sealed record BulkApplyAccommodationSegmentParticipantsRequest(
    Guid[]? ParticipantIds,
    string? OverwriteMode,
    string? AccommodationMode,
    Guid? OverrideAccommodationDocTabId,
    string? RoomNoMode,
    string? RoomNo,
    string? RoomTypeMode,
    string? RoomType,
    string? BoardTypeMode,
    string? BoardType,
    string? PersonNoMode,
    string? PersonNo,
    AccommodationSegmentParticipantRowUpdateRequest[]? RowUpdates);

public sealed record AccommodationSegmentParticipantRowUpdateRequest(
    Guid ParticipantId,
    string? AccommodationMode,
    Guid? OverrideAccommodationDocTabId,
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo);

public sealed record BulkApplyAccommodationSegmentParticipantsErrorDto(
    Guid ParticipantId,
    string Code,
    string Message);

public sealed record BulkApplyAccommodationSegmentParticipantsResponse(
    int AffectedCount,
    int CreatedCount,
    int UpdatedCount,
    int DeletedCount,
    int UnchangedCount,
    BulkApplyAccommodationSegmentParticipantsErrorDto[] Errors);
