using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

public sealed record PortalLoginRequest(string? EventAccessCode, string? TcNo);

public sealed record PortalLoginResponse(
    string PortalSessionToken,
    DateTime ExpiresAt,
    Guid EventId,
    Guid ParticipantId);

public sealed record PortalResolveEventResponse(Guid EventId, string EventTitle);

public sealed record PortalMeResponse(
    PortalEventSummary Event,
    PortalParticipantSummaryFull Participant,
    EventPortalInfo Portal,
    EventScheduleDto Schedule);

public sealed record PortalEventSummary(
    Guid Id,
    string Name,
    string StartDate,
    string EndDate,
    string? LogoUrl);

public sealed record PortalParticipantSummaryFull(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string TcNo,
    string BirthDate,
    string Gender,
    string CheckInCode);
