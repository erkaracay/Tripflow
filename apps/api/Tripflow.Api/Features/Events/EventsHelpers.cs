using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    internal static bool TryParseOptionalTime(string? value, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = parsed;
            return true;
        }

        if (TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            time = parsed;
            return true;
        }

        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            time = parsed;
            return true;
        }

        return false;
    }

    internal static string? FormatTime(TimeOnly? value)
        => value?.ToString("HH:mm");

    internal static List<EventDayEntity> CreateDefaultDays(EventEntity eventEntity)
    {
        var days = new List<EventDayEntity>();
        var totalDays = (eventEntity.EndDate.DayNumber - eventEntity.StartDate.DayNumber) + 1;
        if (totalDays < 1)
        {
            totalDays = 1;
        }

        for (var i = 0; i < totalDays; i++)
        {
            var date = eventEntity.StartDate.AddDays(i);
            days.Add(new EventDayEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = eventEntity.OrganizationId,
                EventId = eventEntity.Id,
                Date = date,
                Title = $"Day {i + 1}",
                Notes = null,
                SortOrder = i + 1,
                IsActive = true
            });
        }

        return days;
    }

    internal static EventScheduleDto ToScheduleDto(
        IEnumerable<EventDayEntity> days,
        IEnumerable<EventActivityEntity> activities)
    {
        var activityGroups = activities
            .GroupBy(x => x.EventDayId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(x => x.StartTime)
                .ThenBy(x => x.Title)
                .Select(ToActivityDto)
                .ToArray());

        var scheduleDays = days
            .OrderBy(x => x.SortOrder)
            .Select(day => new EventScheduleDayDto(
                day.Id,
                day.Date.ToString("yyyy-MM-dd"),
                day.Title,
                day.Notes,
                day.PlacesToVisit,
                day.SortOrder,
                day.IsActive,
                activityGroups.TryGetValue(day.Id, out var list) ? list : Array.Empty<EventActivityDto>()
            ))
            .ToArray();

        return new EventScheduleDto(scheduleDays);
    }

    internal static EventActivityDto ToActivityDto(EventActivityEntity activity)
        => new(
            activity.Id,
            activity.EventDayId,
            activity.Title,
            activity.Type,
            FormatTime(activity.StartTime),
            FormatTime(activity.EndTime),
            activity.LocationName,
            activity.Address,
            activity.Directions,
            activity.Notes,
            activity.CheckInEnabled,
            activity.RequiresCheckIn,
            activity.CheckInMode,
            activity.MenuText,
            activity.ProgramContent,
            activity.SurveyUrl);

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

    private static readonly Regex ValidEventCodeRegex = new("^[A-Z0-9]{6,10}$", RegexOptions.Compiled);

    internal static string NormalizeEventCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var s = value.Trim().ToUpperInvariant();
        return new string(s.Where(c => c >= 'A' && c <= 'Z' || c >= '0' && c <= '9').ToArray());
    }

    internal static bool IsValidEventCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;
        return ValidEventCodeRegex.IsMatch(code);
    }

    internal static async Task<string> GenerateEventAccessCodeAsync(TripflowDbContext db, CancellationToken ct)
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
                .AnyAsync(x => x.EventAccessCode == code, ct);
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
            entity.LogoUrl,
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
