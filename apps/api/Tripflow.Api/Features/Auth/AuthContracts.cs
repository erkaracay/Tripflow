namespace Tripflow.Api.Features.Auth;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record LoginResponse(string AccessToken, string Role, Guid UserId, string? FullName);
public sealed record AuthMeResponse(string Role, Guid UserId, string? FullName);
