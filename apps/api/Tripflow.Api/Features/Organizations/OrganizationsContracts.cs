namespace Tripflow.Api.Features.Organizations;

public sealed record OrganizationListItemDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrganizationCreateRequest(string Name, string? Slug);

public sealed record OrganizationUpdateRequest(string Name, string Slug, bool IsActive);
