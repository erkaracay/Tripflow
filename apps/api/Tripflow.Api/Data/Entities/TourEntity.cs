namespace Tripflow.Api.Data.Entities;

public sealed class TourEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? GuideUserId { get; set; }
    public UserEntity? GuideUser { get; set; }

    public List<ParticipantEntity> Participants { get; set; } = new();
    public TourPortalEntity? Portal { get; set; }
}
