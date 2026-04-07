using System.Text.Json;

namespace Tripflow.Api.Features.Events;

public sealed record ParticipantAccommodationStayDto(
    Guid Id,
    Guid EventAccommodationId,
    string AccommodationTitle,
    JsonElement? AccommodationContent,
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo,
    string? CheckIn,
    string? CheckOut,
    int? NightCount,
    bool IsCurrent,
    string[] Roommates);

public sealed record UpsertParticipantAccommodationStayRequest(
    Guid? EventAccommodationId,
    string? RoomNo,
    string? RoomType,
    string? BoardType,
    string? PersonNo,
    string? CheckIn,
    string? CheckOut);
