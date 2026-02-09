namespace Tripflow.Api.Data.Entities;

public sealed class EventGuideEntity
{
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;

    public Guid GuideUserId { get; set; }
    public UserEntity GuideUser { get; set; } = default!;
}
