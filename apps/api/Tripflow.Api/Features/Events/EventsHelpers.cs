using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Events;

internal static class EventsHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static IResult BadRequest(string message) => Results.BadRequest(new { message });

    internal static bool TryParseDate(string? value, out DateOnly date)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    internal static bool TryParseEventId(string eventIdValue, out Guid eventId, out IResult? error)
    {
        if (!Guid.TryParse(eventIdValue, out eventId))
        {
            error = BadRequest("Invalid event id.");
            return false;
        }

        error = null;
        return true;
    }

    internal static async Task<string> GenerateEventAccessCodeAsync(TripflowDbContext db, Guid organizationId, CancellationToken ct)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var buffer = new char[8];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }

            var code = new string(buffer);
            var exists = await db.Events.AsNoTracking()
                .AnyAsync(x => x.OrganizationId == organizationId && x.EventAccessCode == code, ct);
            if (!exists)
            {
                return code;
            }
        }

        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    internal static EventDto ToDto(EventEntity entity)
        => new(entity.Id,
            entity.Name,
            entity.StartDate.ToString("yyyy-MM-dd"),
            entity.EndDate.ToString("yyyy-MM-dd"),
            entity.GuideUserId,
            entity.IsDeleted,
            entity.EventAccessCode);

    internal static EventPortalInfo? TryDeserializePortal(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<EventPortalInfo>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    internal static EventPortalInfo CreateDefaultPortalInfo(EventDto eventDto)
    {
        var meeting = new MeetingInfo(
            "08:30",
            "Hotel lobby - Grand Central Hotel",
            "https://maps.google.com/?q=Grand+Central+Hotel",
            $"Welcome to {eventDto.Name}. Please arrive 15 minutes early.");

        var links = new[]
        {
            new LinkInfo("Event info pack", "https://example.com/tripflow/event-pack"),
            new LinkInfo("Emergency contacts", "https://example.com/tripflow/emergency"),
            new LinkInfo("Feedback form", "https://example.com/tripflow/feedback")
        };

        var days = new[]
        {
            new DayPlan(1, "Arrival and orientation", new[]
            {
                "Hotel check-in and welcome briefing",
                "City center walk",
                "Group dinner"
            }),
            new DayPlan(2, "Signature sights", new[]
            {
                "Morning guided visit",
                "Free time for lunch",
                "Museum visit"
            }),
            new DayPlan(3, "Local experiences", new[]
            {
                "Market visit",
                "Optional activities",
                "Closing meetup"
            })
        };

        var notes = new[]
        {
            "Bring a reusable water bottle.",
            "Wear comfortable walking shoes.",
            "Share dietary restrictions with the guide."
        };

        return new EventPortalInfo(meeting, links, days, notes);
    }

    internal static async Task<string> GenerateUniqueCheckInCodeAsync(TripflowDbContext db, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = GenerateCheckInCode(8);
            var exists = await db.Participants.AsNoTracking().AnyAsync(x => x.CheckInCode == code, ct);
            if (!exists)
            {
                return code;
            }
        }

        return string.Empty;
    }

    private static string GenerateCheckInCode(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }
}
