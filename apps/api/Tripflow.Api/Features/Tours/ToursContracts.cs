namespace Tripflow.Api.Features.Tours;

public sealed record CreateTourRequest(string? Name, string? StartDate, string? EndDate);
public sealed record UpdateTourRequest(string? Name, string? StartDate, string? EndDate);

public sealed record TourDto(Guid Id, string Name, string StartDate, string EndDate);

public sealed record TourPortalInfo(MeetingInfo Meeting, LinkInfo[] Links, DayPlan[] Days, string[] Notes);
public sealed record MeetingInfo(string Time, string Place, string MapsUrl, string Note);
public sealed record LinkInfo(string Label, string Url);
public sealed record DayPlan(int Day, string Title, string[] Items);

public sealed record CreateParticipantRequest(string? FullName, string? Email, string? Phone);
public sealed record ParticipantDto(Guid Id, string FullName, string? Email, string? Phone, string CheckInCode);

public sealed record CheckInRequest(string? Code, Guid? ParticipantId, string? Method);
public sealed record CheckInResponse(Guid ParticipantId, string ParticipantName, int ArrivedCount, int TotalCount);
