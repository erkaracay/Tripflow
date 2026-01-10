using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Data.Dev;

public static class DevSeed
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<(bool Seeded, string Message)> SeedAsync(TripflowDbContext db, CancellationToken ct)
    {
        if (await db.Tours.AsNoTracking().AnyAsync(ct))
        {
            return (false, "DB dolu. Seed atlandı. Tekrar seed için docker volume’u sıfırla (docker compose down -v).");
        }

        var now = DateTime.UtcNow;

        var tour1 = new TourEntity
        {
            Id = Guid.NewGuid(),
            Name = "Sprint 2 Demo Tour (Istanbul)",
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 1, 12),
            CreatedAt = now
        };

        var tour2 = new TourEntity
        {
            Id = Guid.NewGuid(),
            Name = "Sprint 2 Demo Tour (Ankara)",
            StartDate = new DateOnly(2026, 2, 1),
            EndDate = new DateOnly(2026, 2, 2),
            CreatedAt = now
        };

        db.Tours.AddRange(tour1, tour2);
        await db.SaveChangesAsync(ct);

        db.TourPortals.AddRange(
            new TourPortalEntity
            {
                TourId = tour1.Id,
                PortalJson = CreatePortalJson(tour1.Name, "09:00", "Taksim - Lobby", "https://maps.google.com/?q=Taksim"),
                UpdatedAt = now
            },
            new TourPortalEntity
            {
                TourId = tour2.Id,
                PortalJson = CreatePortalJson(tour2.Name, "10:00", "Kızılay - Lobby", "https://maps.google.com/?q=Kizilay"),
                UpdatedAt = now
            }
        );

        await db.SaveChangesAsync(ct);

        var tour1Participants = new List<ParticipantEntity>();
        for (var i = 1; i <= 20; i++)
        {
            tour1Participants.Add(new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                TourId = tour1.Id,
                FullName = $"Participant {i:00}",
                Email = $"p{i:00}@demo.local",
                Phone = $"+90555{i:000000}",
                CheckInCode = GenerateCheckInCode(8),
                CreatedAt = now
            });
        }

        var tour2Participants = new List<ParticipantEntity>();
        for (var i = 1; i <= 10; i++)
        {
            tour2Participants.Add(new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                TourId = tour2.Id,
                FullName = $"Ankara Participant {i:00}",
                Email = $"a{i:00}@demo.local",
                Phone = $"+90544{i:000000}",
                CheckInCode = GenerateCheckInCode(8),
                CreatedAt = now
            });
        }

        db.Participants.AddRange(tour1Participants);
        db.Participants.AddRange(tour2Participants);
        await db.SaveChangesAsync(ct);

        // tour1: ilk 5 kişi check-in yapılmış olsun -> 5/20 demo hazır
        for (var i = 0; i < 5; i++)
        {
            var p = tour1Participants[i];
            db.CheckIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                TourId = tour1.Id,
                ParticipantId = p.Id,
                CheckedInAt = now.AddMinutes(-(i + 1)),
                Method = "manual"
            });
        }

        await db.SaveChangesAsync(ct);

        return (true, "Seed tamam: 2 tour, 2 portal, 30 participant, tour1 için 5 check-in (5/20).");
    }

    private static string CreatePortalJson(string tourName, string time, string place, string mapsUrl)
    {
        var obj = new
        {
            meeting = new
            {
                time,
                place,
                mapsUrl,
                note = $"Welcome to {tourName}. 15 dk erken gel."
            },
            links = new[]
            {
                new { label = "Info Pack", url = "https://example.com/info" },
                new { label = "Emergency", url = "https://example.com/emergency" }
            },
            days = new[]
            {
                new { day = 1, title = "Day 1", items = new[] { "Meet", "Walk", "Lunch" } },
                new { day = 2, title = "Day 2", items = new[] { "Museum", "Free time", "Dinner" } }
            },
            notes = new[] { "Comfortable shoes", "Water bottle" }
        };

        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static string GenerateCheckInCode(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // I,O,0,1 yok
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
