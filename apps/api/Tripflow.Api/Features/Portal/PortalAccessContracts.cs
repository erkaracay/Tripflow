using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Features.Portal;

public sealed record PortalAccessPolicy(
    bool RequireLast4ForQr,
    bool RequireLast4ForPortal,
    int MaxAttempts,
    int LockMinutes);

public sealed record PortalParticipantSummary(string DisplayName, bool HasPhone);

public sealed record PortalAccessVerifyRequest(string? EventId, string? Pt);
public sealed record PortalAccessVerifyResponse(
    Guid EventId,
    EventPortalInfo Portal,
    PortalAccessPolicy Policy,
    PortalParticipantSummary Participant,
    string? PhoneHint,
    bool IsLocked,
    int LockedForSeconds,
    int AttemptsRemaining);

public sealed record PortalAccessConfirmRequest(string? EventId, string? Pt, string? Last4);
public sealed record PortalAccessConfirmResponse(string SessionToken, DateTime ExpiresAt, PortalAccessPolicy Policy, PortalParticipantSummary Participant);

public sealed record PortalAccessMeResponse(
    Guid EventId,
    Guid ParticipantId,
    string ParticipantName,
    string CheckInCode,
    bool Arrived,
    PortalAccessPolicy Policy);

public sealed record ParticipantPortalAccessResponse(
    string Token,
    bool IsLocked,
    DateTime? LockedUntil,
    int FailedAttempts,
    PortalAccessPolicy Policy);
