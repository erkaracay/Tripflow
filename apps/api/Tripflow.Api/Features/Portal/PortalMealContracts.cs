namespace Tripflow.Api.Features.Portal;

public sealed record PortalMealOptionDto(
    Guid Id,
    string Label,
    int SortOrder);

public sealed record PortalMealSelectionDto(
    Guid GroupId,
    Guid? OptionId,
    string? OtherText,
    string? Note);

public sealed record PortalMealGroupDto(
    Guid GroupId,
    string Title,
    int SortOrder,
    bool AllowOther,
    bool AllowNote,
    PortalMealOptionDto[] Options,
    PortalMealSelectionDto? Selection);

public sealed record PortalMealResponse(
    Guid ActivityId,
    PortalMealGroupDto[] Groups);

public sealed record PortalMealSelectionUpsertItem(
    Guid GroupId,
    Guid? OptionId,
    string? OtherText,
    string? Note);

public sealed record PortalMealSelectionsUpsertRequest(
    PortalMealSelectionUpsertItem[]? Selections);
