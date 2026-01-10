namespace Tripflow.Api.Data.Entities;

public sealed class TourPortalEntity
{
    public Guid TourId { get; set; }
    public TourEntity Tour { get; set; } = default!;

    public string PortalJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
