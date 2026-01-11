using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Tours;

internal static class ToursHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static IResult BadRequest(string message) => Results.BadRequest(new { message });

    internal static bool TryParseDate(string? value, out DateOnly date)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    internal static bool TryParseTourId(string tourIdValue, out Guid tourId, out IResult? error)
    {
        if (!Guid.TryParse(tourIdValue, out tourId))
        {
            error = BadRequest("Invalid tour id.");
            return false;
        }

        error = null;
        return true;
    }

    internal static TourDto ToDto(TourEntity entity)
        => new(entity.Id,
            entity.Name,
            entity.StartDate.ToString("yyyy-MM-dd"),
            entity.EndDate.ToString("yyyy-MM-dd"),
            entity.GuideUserId);

    internal static TourPortalInfo? TryDeserializePortal(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TourPortalInfo>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    internal static TourPortalInfo CreateDefaultPortalInfo(TourDto tour)
    {
        var meeting = new MeetingInfo(
            "08:30",
            "Hotel lobby - Grand Central Hotel",
            "https://maps.google.com/?q=Grand+Central+Hotel",
            $"Welcome to {tour.Name}. Please arrive 15 minutes early.");

        var links = new[]
        {
            new LinkInfo("Tour info pack", "https://example.com/tripflow/tour-pack"),
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
                "Morning guided tour",
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

        return new TourPortalInfo(meeting, links, days, notes);
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
