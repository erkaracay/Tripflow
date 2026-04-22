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
    private static readonly string[] DefaultEquipmentCatalog =
    [
        "Headset",
        "Badge",
        "Radio",
        "Lanyard",
        "Tablet",
        "Power Bank",
        "Welcome Kit"
    ];

    internal static IResult BadRequest(string message) => Results.BadRequest(new { message });

    internal static IResult BadRequest(string code, string field, string message)
        => Results.BadRequest(new { code, field, message });

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

    private static readonly Regex ValidEventCodeRegex = new("^[A-Z0-9]{5,10}$", RegexOptions.Compiled);

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

    internal static bool TryNormalizeTimeZoneId(string? raw, out string normalized, out string errorCode)
    {
        errorCode = "invalid_time_zone_id";
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(trimmed);
            normalized = trimmed;
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
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
            entity.TimeZoneId,
            entity.LogoUrl,
            entity.EventGuides.Select(g => g.GuideUserId).ToArray(),
            entity.IsDeleted,
            entity.EventAccessCode);

    internal static EventContactsDto ToEventContactsDto(EventEntity entity)
        => new(
            NormalizeContactText(entity.GuideName),
            NormalizeContactPhone(entity.GuidePhone),
            NormalizeContactText(entity.LeaderName),
            NormalizeContactPhone(entity.LeaderPhone),
            NormalizeContactPhone(entity.EmergencyPhone),
            NormalizeContactText(entity.WhatsappGroupUrl));

    internal static string? NormalizeContactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    internal static string? NormalizeContactPhone(string? value)
    {
        var trimmed = NormalizeContactText(value);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var hasPlus = trimmed.StartsWith("+", StringComparison.Ordinal);
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return trimmed;
        }

        return hasPlus ? $"+{digits}" : digits;
    }

    internal static bool TryNormalizeHttpsAbsoluteUrl(string? value, out string? normalizedUrl)
    {
        normalizedUrl = NormalizeContactText(value);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            normalizedUrl = null;
            return true;
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

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

    internal static List<EventDocTabEntity> CreateDefaultDocTabs(EventEntity entity, DateTime createdAtUtc)
    {
        // Hotel tabs are lazy-created when the admin adds the first accommodation
        // (via "Konaklama ekle" on the room-ops page or via participant import).
        // Seeding an empty Hotel tab on every event created friction in the delete
        // flow and visual noise in the participant portal; only system-required
        // singletons (Insurance, Transfer) are seeded here.
        var insuranceContent = JsonSerializer.Serialize(new
        {
            companyName = string.Empty,
            policyNo = string.Empty,
            startDate = string.Empty,
            endDate = string.Empty
        });

        var transferContent = JsonSerializer.Serialize(new { });

        return
        [
            new EventDocTabEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = entity.OrganizationId,
                EventId = entity.Id,
                Title = "Insurance",
                Type = "Insurance",
                SortOrder = 1,
                IsActive = true,
                ContentJson = insuranceContent,
                CreatedAt = createdAtUtc
            },
            new EventDocTabEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = entity.OrganizationId,
                EventId = entity.Id,
                Title = "Transfer",
                Type = "Transfer",
                SortOrder = 2,
                IsActive = true,
                ContentJson = transferContent,
                CreatedAt = createdAtUtc
            }
        ];
    }

    internal static List<EventItemEntity> CreateDefaultEventItems(EventEntity entity, int count)
    {
        var safeCount = Math.Clamp(count, 0, 10);
        var items = new List<EventItemEntity>(safeCount);

        for (var i = 0; i < safeCount; i++)
        {
            var itemName = i < DefaultEquipmentCatalog.Length
                ? DefaultEquipmentCatalog[i]
                : $"Equipment {i + 1}";

            items.Add(new EventItemEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = entity.OrganizationId,
                EventId = entity.Id,
                Type = itemName,
                Title = "Equipment",
                Name = itemName,
                IsActive = true,
                SortOrder = i + 1
            });
        }

        return items;
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
