namespace Tripflow.Api.Data.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? FullName { get; set; }
    public string PasswordHash { get; set; } = default!;
    public string Role { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    public List<TourEntity> GuidedTours { get; set; } = new();
}
