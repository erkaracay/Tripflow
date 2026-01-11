namespace Tripflow.Api.Features.Users;

public sealed record UserListItemDto(Guid Id, string Email, string? FullName, string Role);
