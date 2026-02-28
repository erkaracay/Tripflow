namespace Tripflow.Api.Features.Users;

public sealed record UserListItemDto(Guid Id, string Email, string? FullName, string Role);

public sealed record UserUpsertResponseDto(UserListItemDto User, string Action);

public sealed record CreateUserRequest(string Email, string? Password, string Role, Guid OrganizationId, string? FullName);

public sealed record CreateGuideRequest(string Email, string? Password, string? FullName);

public sealed record ChangePasswordRequest(string NewPassword);
