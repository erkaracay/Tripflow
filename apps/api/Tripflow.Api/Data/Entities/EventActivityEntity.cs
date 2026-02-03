namespace Tripflow.Api.Data.Entities;

public sealed class EventActivityEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = default!;
    public Guid EventId { get; set; }
    public EventEntity Event { get; set; } = default!;
    public Guid EventDayId { get; set; }
    public EventDayEntity Day { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Type { get; set; } = "Other";
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? LocationName { get; set; }
    public string? Address { get; set; }
    public string? Directions { get; set; }
    public string? Notes { get; set; }
    public bool CheckInEnabled { get; set; }
    public string CheckInMode { get; set; } = "EntryOnly";
    public string? MenuText { get; set; }
    public string? SurveyUrl { get; set; }
}
