namespace Tripflow.Api.Features.Events;

public sealed record MealOptionDto(
    Guid Id,
    string Label,
    int SortOrder,
    bool IsActive);

public sealed record MealGroupDto(
    Guid Id,
    Guid ActivityId,
    string Title,
    int SortOrder,
    bool AllowOther,
    bool AllowNote,
    bool IsActive,
    MealOptionDto[] Options);

public sealed record MealGroupsResponse(
    Guid ActivityId,
    MealGroupDto[] Groups);

public sealed record CreateMealGroupRequest(
    string? Title,
    int? SortOrder,
    bool? AllowOther,
    bool? AllowNote,
    bool? IsActive);

public sealed record UpdateMealGroupRequest(
    string? Title,
    int? SortOrder,
    bool? AllowOther,
    bool? AllowNote,
    bool? IsActive);

public sealed record CreateMealOptionRequest(
    string? Label,
    int? SortOrder,
    bool? IsActive);

public sealed record UpdateMealOptionRequest(
    string? Label,
    int? SortOrder,
    bool? IsActive);

public sealed record MealSummaryCountDto(
    Guid? OptionId,
    string Label,
    int Count);

public sealed record MealSummaryGroupDto(
    Guid GroupId,
    string Title,
    bool AllowOther,
    bool AllowNote,
    MealSummaryCountDto[] Counts,
    int NoteCount);

public sealed record MealSummaryResponse(
    Guid ActivityId,
    MealSummaryGroupDto[] Groups);

public sealed record MealChoiceParticipantDto(
    Guid Id,
    string FullName,
    string? RoomNo,
    string Phone);

public sealed record MealChoiceListItemDto(
    MealChoiceParticipantDto Participant,
    Guid? OptionId,
    string? OptionLabel,
    string? OtherText,
    string? Note,
    string UpdatedAt);

public sealed record MealChoiceListResponse(
    int Page,
    int PageSize,
    int Total,
    MealChoiceListItemDto[] Items);

public sealed record MealShareSummaryCountDto(string Label, int Count);

public sealed record MealShareSummaryGroupDto(string Title, MealShareSummaryCountDto[] Counts);

public sealed record MealShareSummarySpecialRequestDto(
    string ParticipantName,
    string? RoomNo,
    string? OtherText,
    string? Note);

public sealed record MealShareSummaryResponse(
    string ActivityTitle,
    MealShareSummaryGroupDto[] Groups,
    MealShareSummarySpecialRequestDto[] SpecialRequests);
