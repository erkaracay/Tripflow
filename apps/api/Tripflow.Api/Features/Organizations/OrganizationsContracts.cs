namespace Tripflow.Api.Features.Organizations;

public sealed record OrganizationListItemDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool IsDeleted,
    bool RequireLast4ForQr,
    bool RequireLast4ForPortal,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool IsDeleted,
    bool RequireLast4ForQr,
    bool RequireLast4ForPortal,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrganizationCreateRequest(string Name, string? Slug, bool? RequireLast4ForQr, bool? RequireLast4ForPortal);

public sealed record OrganizationUpdateRequest(string Name, string Slug, bool IsActive, bool RequireLast4ForQr, bool RequireLast4ForPortal);
