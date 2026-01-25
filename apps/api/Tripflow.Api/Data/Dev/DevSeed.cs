using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Portal;

namespace Tripflow.Api.Data.Dev;

public static class DevSeed
{
    private sealed class SeedState
    {
        public bool Seeded { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SampleNames =
    [
        "Ayşe Demir",
        "Mehmet Kaya",
        "Elif Yılmaz",
        "Can Arslan",
        "Zeynep Acar",
        "Mert Yıldız",
        "Ece Şahin",
        "Kerem Polat",
        "Deniz Turan",
        "Seda Koç",
        "Emre Çakır",
        "Melis Aydın",
        "Ozan Aslan",
        "Nazlı Güneş",
        "Barış Eren",
        "İpek Tan",
        "Bora Demir",
        "Selin Öz",
        "Burak Yılmaz",
        "Derya Akın",
        "Tuna Arda",
        "Sevgi Koçak",
        "Ceren Ateş",
        "Kaan Gül",
        "Gökçe Sarı",
        "Tolga Bozkurt",
        "Eda Yaşar",
        "Umut Erdem",
        "Pelin Aksoy",
        "Hakan Köse",
        "Ege Karaman",
        "Aslı Kaplan",
        "Yasemin Er",
        "Cemal Ak",
        "Hande Ar",
        "İlker Tan"
    ];

    public static async Task<(bool Seeded, string Message)> SeedAsync(TripflowDbContext db, CancellationToken ct)
    {
        var state = new SeedState();
        var now = DateTime.UtcNow;
        var hasher = new PasswordHasher<UserEntity>();

        var orgA = await GetOrCreateOrg(db, "Mavi Rota Travel", "org-a", now, ct, state);
        var orgB = await GetOrCreateOrg(db, "Atlas Travel", "org-b", now, ct, state);

        var superAdmin = await UpsertUser(
            db,
            hasher,
            "superadmin@demo.local",
            "Super Admin",
            "SuperAdmin",
            null,
            "admin123",
            now,
            ct,
            state);

        var adminA = await UpsertUser(
            db,
            hasher,
            "adminA@demo.local",
            "Org A Admin",
            "AgencyAdmin",
            orgA.Id,
            "admin123",
            now,
            ct,
            state);

        var guideA = await UpsertUser(
            db,
            hasher,
            "guideA@demo.local",
            "Org A Guide",
            "Guide",
            orgA.Id,
            "guide123",
            now,
            ct,
            state);

        var adminB = await UpsertUser(
            db,
            hasher,
            "adminB@demo.local",
            "Org B Admin",
            "AgencyAdmin",
            orgB.Id,
            "admin123",
            now,
            ct,
            state);

        var guideB = await UpsertUser(
            db,
            hasher,
            "guideB@demo.local",
            "Org B Guide",
            "Guide",
            orgB.Id,
            "guide123",
            now,
            ct,
            state);

        await db.SaveChangesAsync(ct);

        var tourA1 = await GetOrCreateTour(db, orgA.Id, "Kapadokya Bahar Turu", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), guideA.Id, now, ct, state);
        var tourA2 = await GetOrCreateTour(db, orgA.Id, "Ege Kiyilari Hafta Sonu", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2), guideA.Id, now, ct, state);

        var tourB1 = await GetOrCreateTour(db, orgB.Id, "Karadeniz Yayla Rotasi", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12), guideB.Id, now, ct, state);
        var tourB2 = await GetOrCreateTour(db, orgB.Id, "Istanbul Tarih Turu", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), guideB.Id, now, ct, state);

        await db.SaveChangesAsync(ct);

        await EnsurePortalAsync(db, tourA1, orgA.Id, now, ct, state);
        await EnsurePortalAsync(db, tourA2, orgA.Id, now, ct, state);
        await EnsurePortalAsync(db, tourB1, orgB.Id, now, ct, state);
        await EnsurePortalAsync(db, tourB2, orgB.Id, now, ct, state);

        await EnsureParticipantsAsync(db, tourA1, orgA.Id, "A1", 30, now, ct, state);
        await EnsureParticipantsAsync(db, tourA2, orgA.Id, "A2", 30, now, ct, state);
        await EnsureParticipantsAsync(db, tourB1, orgB.Id, "B1", 30, now, ct, state);
        await EnsureParticipantsAsync(db, tourB2, orgB.Id, "B2", 30, now, ct, state);

        await EnsureParticipantAccessAsync(db, tourA1, orgA.Id, now, ct, state);
        await EnsureParticipantAccessAsync(db, tourA2, orgA.Id, now, ct, state);
        await EnsureParticipantAccessAsync(db, tourB1, orgB.Id, now, ct, state);
        await EnsureParticipantAccessAsync(db, tourB2, orgB.Id, now, ct, state);

        await EnsureCheckInsAsync(db, tourA1, orgA.Id, now, ct, state);
        await EnsureCheckInsAsync(db, tourB1, orgB.Id, now, ct, state);

        await db.SaveChangesAsync(ct);

        var message = state.Seeded
            ? "Seed tamam: 2 org, 5 kullanıcı (superadmin/admin/guide), 4 tour, 120 participant, check-in örnekleri."
            : "Seed zaten yapılmış. Mevcut demo kayıtları korundu.";

        return (state.Seeded, message);
    }

    private static async Task<OrganizationEntity> GetOrCreateOrg(
        TripflowDbContext db,
        string name,
        string slug,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (org is not null)
        {
            var updated = false;
            if (!string.Equals(org.Name, name, StringComparison.Ordinal))
            {
                org.Name = name;
                updated = true;
            }

            if (!org.RequireLast4ForQr)
            {
                org.RequireLast4ForQr = true;
                updated = true;
            }

            if (org.RequireLast4ForPortal)
            {
                org.RequireLast4ForPortal = false;
                updated = true;
            }

            if (!org.IsActive)
            {
                org.IsActive = true;
                updated = true;
            }

            if (org.IsDeleted)
            {
                org.IsDeleted = false;
                updated = true;
            }

            if (updated)
            {
                org.UpdatedAt = now;
                db.Organizations.Update(org);
                state.Seeded = true;
            }
            return org;
        }

        org = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true,
            IsDeleted = false,
            RequireLast4ForQr = true,
            RequireLast4ForPortal = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Organizations.Add(org);
        state.Seeded = true;
        return org;
    }

    private static async Task<UserEntity> UpsertUser(
        TripflowDbContext db,
        PasswordHasher<UserEntity> hasher,
        string email,
        string fullName,
        string role,
        Guid? organizationId,
        string password,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, ct);
        if (user is null)
        {
            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = fullName,
                Role = role,
                OrganizationId = organizationId,
                CreatedAt = now
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            state.Seeded = true;
            return user;
        }

        var updated = false;
        if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
        {
            user.FullName = fullName;
            updated = true;
        }

        if (!string.Equals(user.Role, role, StringComparison.Ordinal))
        {
            user.Role = role;
            updated = true;
        }

        if (user.OrganizationId != organizationId)
        {
            user.OrganizationId = organizationId;
            updated = true;
        }

        user.PasswordHash = hasher.HashPassword(user, password);
        updated = true;

        if (updated)
        {
            db.Users.Update(user);
            state.Seeded = true;
        }

        return user;
    }

    private static async Task<TourEntity> GetOrCreateTour(
        TripflowDbContext db,
        Guid organizationId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        Guid guideUserId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var tour = await db.Tours.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Name == name, ct);
        if (tour is not null)
        {
            if (tour.GuideUserId != guideUserId)
            {
                tour.GuideUserId = guideUserId;
                db.Tours.Update(tour);
                state.Seeded = true;
            }
            return tour;
        }

        tour = new TourEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = now,
            GuideUserId = guideUserId
        };

        db.Tours.Add(tour);
        state.Seeded = true;
        return tour;
    }

    private static async Task EnsurePortalAsync(
        TripflowDbContext db,
        TourEntity tour,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var portalExists = await db.TourPortals.AnyAsync(x => x.TourId == tour.Id, ct);
        if (portalExists)
        {
            return;
        }

        db.TourPortals.Add(new TourPortalEntity
        {
            TourId = tour.Id,
            OrganizationId = organizationId,
            PortalJson = CreatePortalJson(tour.Name, "09:00", "Lobby", "https://maps.google.com/?q=Lobby"),
            UpdatedAt = now
        });
        state.Seeded = true;
    }

    private static async Task EnsureParticipantsAsync(
        TripflowDbContext db,
        TourEntity tour,
        Guid organizationId,
        string prefix,
        int count,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var existing = await db.Participants.AsNoTracking().AnyAsync(x => x.TourId == tour.Id, ct);
        if (existing)
        {
            return;
        }

        var participants = new List<ParticipantEntity>();
        for (var i = 1; i <= count; i++)
        {
            var code = await GenerateUniqueCheckInCodeAsync(db, ct);
            var name = SampleNames[(i - 1) % SampleNames.Length];
            var phoneDigits = 5300000000 + i;
            participants.Add(new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                TourId = tour.Id,
                OrganizationId = organizationId,
                FullName = name,
                Email = BuildDemoEmail(prefix, i),
                Phone = $"+90{phoneDigits:0000000000}",
                CheckInCode = code,
                CreatedAt = now
            });
        }

        db.Participants.AddRange(participants);
        state.Seeded = true;
    }

    private static async Task EnsureCheckInsAsync(
        TripflowDbContext db,
        TourEntity tour,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var alreadyCheckedIn = await db.CheckIns.AsNoTracking().AnyAsync(x => x.TourId == tour.Id, ct);
        if (alreadyCheckedIn)
        {
            return;
        }

        var participants = await db.Participants.AsNoTracking()
            .Where(x => x.TourId == tour.Id)
            .OrderBy(x => x.FullName)
            .Take(5)
            .ToListAsync(ct);

        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];
            db.CheckIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                TourId = tour.Id,
                ParticipantId = participant.Id,
                OrganizationId = organizationId,
                CheckedInAt = now.AddMinutes(-(i + 1)),
                Method = "manual"
            });
        }

        if (participants.Count > 0)
        {
            state.Seeded = true;
        }
    }

    private static async Task EnsureParticipantAccessAsync(
        TripflowDbContext db,
        TourEntity tour,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var participants = await db.Participants.AsNoTracking()
            .Where(x => x.TourId == tour.Id && x.OrganizationId == organizationId)
            .ToListAsync(ct);

        if (participants.Count == 0)
        {
            return;
        }

        var participantIds = participants.Select(x => x.Id).ToList();
        var accessInfo = await db.ParticipantAccesses.AsNoTracking()
            .Where(x => participantIds.Contains(x.ParticipantId))
            .GroupBy(x => x.ParticipantId)
            .ToDictionaryAsync(
                g => g.Key,
                g => new
                {
                    HasActive = g.Any(x => x.RevokedAt == null),
                    MaxVersion = g.Max(x => x.Version)
                },
                ct);

        var added = false;
        foreach (var participant in participants)
        {
            if (accessInfo.TryGetValue(participant.Id, out var info) && info.HasActive)
            {
                continue;
            }

            var secret = PortalAccessHelpers.GenerateSecret();
            var tokenId = Guid.NewGuid();
            var version = accessInfo.TryGetValue(participant.Id, out info) ? info.MaxVersion + 1 : 1;

            db.ParticipantAccesses.Add(new ParticipantAccessEntity
            {
                Id = tokenId,
                OrganizationId = organizationId,
                ParticipantId = participant.Id,
                Version = version,
                Secret = secret,
                SecretHash = PortalAccessHelpers.HashSecret(secret),
                CreatedAt = now
            });

            added = true;
        }

        if (added)
        {
            state.Seeded = true;
        }
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
                note = $"Welcome to {tourName}. Please arrive 15 minutes early."
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

    private static string BuildDemoEmail(string prefix, int index)
    {
        var safePrefix = prefix.Trim().ToLowerInvariant();
        return $"traveler.{safePrefix}.{index:00}@demo.local";
    }

    private static async Task<string> GenerateUniqueCheckInCodeAsync(TripflowDbContext db, CancellationToken ct)
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

        return GenerateCheckInCode(8);
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
