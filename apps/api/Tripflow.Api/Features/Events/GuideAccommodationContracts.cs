namespace Tripflow.Api.Features.Events;

public sealed record GuideAccommodationOptionDto(
    Guid Id,
    string Title);

public sealed record GuideAccommodationParticipantDto(
    Guid ParticipantId,
    string FullName,
    string TcNo,
    Guid EffectiveAccommodationDocTabId,
    string EffectiveAccommodationTitle,
    bool UsesOverride,
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo,
    string[] Roommates);

public sealed record GuideAccommodationParticipantResponseDto(
    int Page,
    int PageSize,
    int Total,
    GuideAccommodationParticipantDto[] Items,
    GuideAccommodationOptionDto[] AvailableAccommodations);
