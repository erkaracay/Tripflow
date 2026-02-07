using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Events;
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

        var eventA1 = await GetOrCreateEvent(db, orgA.Id, "Kapadokya Bahar Etkinliği", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), guideA.Id, now, ct, state);
        var eventA2 = await GetOrCreateEvent(db, orgA.Id, "Ege Kıyıları Hafta Sonu", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2), guideA.Id, now, ct, state);

        var eventB1 = await GetOrCreateEvent(db, orgB.Id, "Karadeniz Yayla Rotası", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12), guideB.Id, now, ct, state);
        var eventB2 = await GetOrCreateEvent(db, orgB.Id, "İstanbul Tarih Etkinliği", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), guideB.Id, now, ct, state);

        await db.SaveChangesAsync(ct);

        await EnsurePortalAsync(db, eventA1, orgA.Id, now, ct, state);
        await EnsurePortalAsync(db, eventA2, orgA.Id, now, ct, state);
        await EnsurePortalAsync(db, eventB1, orgB.Id, now, ct, state);
        await EnsurePortalAsync(db, eventB2, orgB.Id, now, ct, state);

        await EnsureDocTabsAsync(db, eventA1, orgA.Id, now, ct, state);
        await EnsureDocTabsAsync(db, eventA2, orgA.Id, now, ct, state);
        await EnsureDocTabsAsync(db, eventB1, orgB.Id, now, ct, state);
        await EnsureDocTabsAsync(db, eventB2, orgB.Id, now, ct, state);

        await EnsureScheduleAsync(db, eventA1, orgA.Id, now, ct, state);
        await EnsureScheduleAsync(db, eventA2, orgA.Id, now, ct, state);
        await EnsureScheduleAsync(db, eventB1, orgB.Id, now, ct, state);
        await EnsureScheduleAsync(db, eventB2, orgB.Id, now, ct, state);

        await EnsureParticipantsAsync(db, eventA1, orgA.Id, "A1", 30, now, ct, state);
        await EnsureParticipantsAsync(db, eventA2, orgA.Id, "A2", 30, now, ct, state);
        await EnsureParticipantsAsync(db, eventB1, orgB.Id, "B1", 30, now, ct, state);
        await EnsureParticipantsAsync(db, eventB2, orgB.Id, "B2", 30, now, ct, state);

        await EnsureCheckInsAsync(db, eventA1, orgA.Id, now, ct, state);
        await EnsureCheckInsAsync(db, eventB1, orgB.Id, now, ct, state);

        await db.SaveChangesAsync(ct);

        var message = state.Seeded
            ? "Seed tamam: 2 org, 5 kullanıcı (superadmin/admin/guide), 4 etkinlik, 120 katılımcı, check-in örnekleri."
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

    private static async Task<EventEntity> GetOrCreateEvent(
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
        var eventEntity = await db.Events.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Name == name, ct);
        if (eventEntity is not null)
        {
            if (eventEntity.GuideUserId != guideUserId)
            {
                eventEntity.GuideUserId = guideUserId;
                db.Events.Update(eventEntity);
                state.Seeded = true;
            }
            if (string.IsNullOrWhiteSpace(eventEntity.EventAccessCode))
            {
                eventEntity.EventAccessCode = await EventsHelpers.GenerateEventAccessCodeAsync(db, ct);
                db.Events.Update(eventEntity);
                state.Seeded = true;
            }
            return eventEntity;
        }

        eventEntity = new EventEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            EventAccessCode = await EventsHelpers.GenerateEventAccessCodeAsync(db, ct),
            CreatedAt = now,
            GuideUserId = guideUserId
        };

        db.Events.Add(eventEntity);
        state.Seeded = true;
        return eventEntity;
    }

    private static async Task EnsurePortalAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var portalExists = await db.EventPortals.AnyAsync(x => x.EventId == eventEntity.Id, ct);
        if (portalExists)
        {
            return;
        }

        db.EventPortals.Add(new EventPortalEntity
        {
            EventId = eventEntity.Id,
            OrganizationId = organizationId,
            PortalJson = CreatePortalJson(eventEntity.Name, "09:00", "Lobby", "https://maps.google.com/?q=Lobby"),
            UpdatedAt = now
        });
        state.Seeded = true;
    }

    private static async Task EnsureParticipantsAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        Guid organizationId,
        string prefix,
        int count,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var existing = await db.Participants.AsNoTracking().AnyAsync(x => x.EventId == eventEntity.Id, ct);
        if (existing)
        {
            var existingParticipants = await db.Participants
                .Where(x => x.EventId == eventEntity.Id)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);

            if (existingParticipants.Count > 0)
            {
                for (var i = 0; i < existingParticipants.Count; i++)
                {
                    var participant = existingParticipants[i];
                    var index = i + 1;
                    var phoneDigits = 5300000000 + index;

                    if (string.IsNullOrWhiteSpace(participant.Phone))
                    {
                        participant.Phone = $"+90{phoneDigits:0000000000}";
                    }

                    if (string.IsNullOrWhiteSpace(participant.TcNo))
                    {
                        participant.TcNo = (10000000000L + index).ToString();
                    }

                    if (participant.BirthDate == default)
                    {
                        participant.BirthDate = new DateOnly(1990, 1, 1).AddDays(index);
                    }

                    if (!Enum.IsDefined(typeof(ParticipantGender), participant.Gender))
                    {
                        participant.Gender = (index % 3) switch
                        {
                            0 => ParticipantGender.Female,
                            1 => ParticipantGender.Male,
                            _ => ParticipantGender.Other
                        };
                    }
                }

                await db.SaveChangesAsync(ct);
                state.Seeded = true;
            }

            await EnsureParticipantDetailsAsync(db, eventEntity, organizationId, existingParticipants, now, ct, state);
            return;
        }

        var participants = new List<ParticipantEntity>();
        for (var i = 1; i <= count; i++)
        {
            var code = await GenerateUniqueCheckInCodeAsync(db, ct);
            var name = SampleNames[(i - 1) % SampleNames.Length];
            var phoneDigits = 5300000000 + i;
            var tcNo = (10000000000L + i).ToString();
            var birthDate = new DateOnly(1990, 1, 1).AddDays(i);
            var gender = (i % 3) switch
            {
                0 => ParticipantGender.Female,
                1 => ParticipantGender.Male,
                _ => ParticipantGender.Other
            };
            participants.Add(new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                OrganizationId = organizationId,
                FullName = name,
                Email = BuildDemoEmail(prefix, i),
                Phone = $"+90{phoneDigits:0000000000}",
                TcNo = tcNo,
                BirthDate = birthDate,
                Gender = gender,
                CheckInCode = code,
                CreatedAt = now
            });
        }

        db.Participants.AddRange(participants);
        state.Seeded = true;

        await EnsureParticipantDetailsAsync(db, eventEntity, organizationId, participants, now, ct, state);
    }

    private static async Task EnsureParticipantDetailsAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        Guid organizationId,
        IReadOnlyList<ParticipantEntity> participants,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        if (participants.Count == 0)
        {
            return;
        }

        var participantIds = participants.Select(x => x.Id).ToArray();
        var existingDetails = await db.ParticipantDetails
            .Where(x => participantIds.Contains(x.ParticipantId))
            .ToListAsync(ct);

        var detailsById = existingDetails.ToDictionary(x => x.ParticipantId, x => x);

        var airlines = new[] { "THY", "Pegasus", "AJet" };
        var airports = new[] { "IST", "SAW", "ESB", "ADB" };
        var cities = new[] { "İstanbul", "Ankara", "İzmir", "Antalya" };
        var boardTypes = new[] { "BB", "HB", "AI", "RO" };
        var roomTypes = new[] { "Single", "Double", "Triple" };

        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];
            var index = i + 1;
            var detail = detailsById.TryGetValue(participant.Id, out var existing)
                ? existing
                : new ParticipantDetailsEntity { ParticipantId = participant.Id };

            var updated = false;

            if (string.IsNullOrWhiteSpace(detail.RoomNo))
            {
                detail.RoomNo = $"{100 + (index % 20)}";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.RoomType))
            {
                detail.RoomType = roomTypes[index % roomTypes.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.BoardType))
            {
                detail.BoardType = boardTypes[index % boardTypes.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.PersonNo))
            {
                detail.PersonNo = ((index % 3) + 1).ToString();
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.AgencyName))
            {
                detail.AgencyName = "Tripflow Travel";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.City))
            {
                detail.City = cities[index % cities.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.FlightCity))
            {
                detail.FlightCity = cities[(index + 1) % cities.Length];
                updated = true;
            }
            if (detail.HotelCheckInDate is null)
            {
                detail.HotelCheckInDate = eventEntity.StartDate;
                updated = true;
            }
            if (detail.HotelCheckOutDate is null)
            {
                detail.HotelCheckOutDate = eventEntity.EndDate;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.TicketNo))
            {
                var prefix = string.IsNullOrWhiteSpace(eventEntity.Name)
                    ? "E"
                    : eventEntity.Name.Trim()[0].ToString().ToUpperInvariant();
                detail.TicketNo = $"{prefix}{index:0000}";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.AttendanceStatus))
            {
                detail.AttendanceStatus = "Confirmed";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.InsuranceCompanyName))
            {
                detail.InsuranceCompanyName = "AXA";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.InsurancePolicyNo))
            {
                detail.InsurancePolicyNo = $"POL-{index:0000}";
                updated = true;
            }
            if (detail.InsuranceStartDate is null)
            {
                detail.InsuranceStartDate = eventEntity.StartDate;
                updated = true;
            }
            if (detail.InsuranceEndDate is null)
            {
                detail.InsuranceEndDate = eventEntity.EndDate;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(detail.ArrivalAirline))
            {
                detail.ArrivalAirline = airlines[index % airlines.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalDepartureAirport))
            {
                detail.ArrivalDepartureAirport = airports[index % airports.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalArrivalAirport))
            {
                detail.ArrivalArrivalAirport = airports[(index + 1) % airports.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalFlightCode))
            {
                detail.ArrivalFlightCode = $"TF{100 + index}";
                updated = true;
            }
            if (detail.ArrivalDepartureTime is null)
            {
                detail.ArrivalDepartureTime = new TimeOnly(8 + (index % 6), 30);
                updated = true;
            }
            if (detail.ArrivalArrivalTime is null)
            {
                detail.ArrivalArrivalTime = new TimeOnly(10 + (index % 6), 5);
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalPnr))
            {
                detail.ArrivalPnr = $"PNR{index:0000}";
                updated = true;
            }
            if (detail.ArrivalBaggagePieces is null)
            {
                detail.ArrivalBaggagePieces = (index % 2) + 1;
                updated = true;
            }
            if (detail.ArrivalBaggageTotalKg is null)
            {
                detail.ArrivalBaggageTotalKg = (index % 2 == 0) ? 23 : 30;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalBaggageAllowance))
            {
                detail.ArrivalBaggageAllowance = $"{detail.ArrivalBaggagePieces} pc {detail.ArrivalBaggageTotalKg} kg";
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(detail.ReturnAirline))
            {
                detail.ReturnAirline = airlines[(index + 1) % airlines.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnDepartureAirport))
            {
                detail.ReturnDepartureAirport = airports[(index + 1) % airports.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnArrivalAirport))
            {
                detail.ReturnArrivalAirport = airports[index % airports.Length];
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnFlightCode))
            {
                detail.ReturnFlightCode = $"TF{200 + index}";
                updated = true;
            }
            if (detail.ReturnDepartureTime is null)
            {
                detail.ReturnDepartureTime = new TimeOnly(17 + (index % 4), 0);
                updated = true;
            }
            if (detail.ReturnArrivalTime is null)
            {
                detail.ReturnArrivalTime = new TimeOnly(19 + (index % 4), 20);
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnPnr))
            {
                detail.ReturnPnr = $"RPNR{index:0000}";
                updated = true;
            }
            if (detail.ReturnBaggagePieces is null)
            {
                detail.ReturnBaggagePieces = (index % 2) + 1;
                updated = true;
            }
            if (detail.ReturnBaggageTotalKg is null)
            {
                detail.ReturnBaggageTotalKg = (index % 2 == 0) ? 23 : 30;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnBaggageAllowance))
            {
                detail.ReturnBaggageAllowance = $"{detail.ReturnBaggagePieces} pc {detail.ReturnBaggageTotalKg} kg";
                updated = true;
            }

            if (detail.ArrivalTransferPickupTime is null)
            {
                detail.ArrivalTransferPickupTime = new TimeOnly(7 + (index % 4), 15);
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalTransferPickupPlace))
            {
                detail.ArrivalTransferPickupPlace = "Havalimanı Terminal 1";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalTransferDropoffPlace))
            {
                detail.ArrivalTransferDropoffPlace = "Otel giriş kapısı";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalTransferVehicle))
            {
                detail.ArrivalTransferVehicle = "Mercedes Sprinter";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalTransferPlate))
            {
                detail.ArrivalTransferPlate = $"34 TF {200 + index:000}";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalTransferDriverInfo))
            {
                detail.ArrivalTransferDriverInfo = $"Sürücü {index % 5 + 1} · +90 5{index % 9}0 123 45 67";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ArrivalTransferNote))
            {
                detail.ArrivalTransferNote = "Karşılama tabelası Tripflow";
                updated = true;
            }

            if (detail.ReturnTransferPickupTime is null)
            {
                detail.ReturnTransferPickupTime = new TimeOnly(18 + (index % 3), 0);
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnTransferPickupPlace))
            {
                detail.ReturnTransferPickupPlace = "Otel lobisi";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnTransferDropoffPlace))
            {
                detail.ReturnTransferDropoffPlace = "Havalimanı Terminal 2";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnTransferVehicle))
            {
                detail.ReturnTransferVehicle = "Mercedes Vito";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnTransferPlate))
            {
                detail.ReturnTransferPlate = $"34 TF {500 + index:000}";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnTransferDriverInfo))
            {
                detail.ReturnTransferDriverInfo = $"Sürücü {index % 4 + 1} · +90 5{index % 9}0 765 43 21";
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(detail.ReturnTransferNote))
            {
                detail.ReturnTransferNote = "Lobide 15 dk önce buluşma";
                updated = true;
            }

            if (existing is null)
            {
                db.ParticipantDetails.Add(detail);
                updated = true;
            }

            if (updated)
            {
                state.Seeded = true;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureDocTabsAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var hasTabs = await db.EventDocTabs
            .AnyAsync(x => x.EventId == eventEntity.Id && x.OrganizationId == organizationId, ct);
        if (hasTabs)
        {
            return;
        }

        var hotelContent = JsonSerializer.Serialize(new
        {
            hotelName = $"{eventEntity.Name} Otel",
            address = "Merkez Mah. 10. Sokak No:5",
            phone = "+90 212 555 0000",
            checkInNote = "Giriş 14:00 itibarıyla",
            checkOutNote = "Çıkış 12:00"
        });

        var insuranceContent = JsonSerializer.Serialize(new
        {
            companyName = string.Empty,
            policyNo = string.Empty,
            startDate = string.Empty,
            endDate = string.Empty
        });

        var transferContent = JsonSerializer.Serialize(new { });

        db.EventDocTabs.AddRange(
            new EventDocTabEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventEntity.Id,
                Title = "Hotel",
                Type = "Hotel",
                SortOrder = 1,
                IsActive = true,
                ContentJson = hotelContent,
                CreatedAt = now
            },
            new EventDocTabEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventEntity.Id,
                Title = "Insurance",
                Type = "Insurance",
                SortOrder = 2,
                IsActive = false,
                ContentJson = insuranceContent,
                CreatedAt = now
            },
            new EventDocTabEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventEntity.Id,
                Title = "Transfer",
                Type = "Transfer",
                SortOrder = 3,
                IsActive = true,
                ContentJson = transferContent,
                CreatedAt = now
            });

        state.Seeded = true;
    }

    private static async Task EnsureCheckInsAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var alreadyCheckedIn = await db.CheckIns.AsNoTracking().AnyAsync(x => x.EventId == eventEntity.Id, ct);
        if (alreadyCheckedIn)
        {
            return;
        }

        var participants = await db.Participants.AsNoTracking()
            .Where(x => x.EventId == eventEntity.Id)
            .OrderBy(x => x.FullName)
            .Take(5)
            .ToListAsync(ct);

        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];
            db.CheckIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
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

    private static async Task EnsureScheduleAsync(
        TripflowDbContext db,
        EventEntity eventEntity,
        Guid organizationId,
        DateTime now,
        CancellationToken ct,
        SeedState state)
    {
        var days = await db.EventDays
            .Where(x => x.EventId == eventEntity.Id && x.OrganizationId == organizationId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        if (days.Count == 0)
        {
            days = EventsHelpers.CreateDefaultDays(eventEntity);
            db.EventDays.AddRange(days);
            state.Seeded = true;
        }

        var hasActivities = await db.EventActivities.AsNoTracking()
            .AnyAsync(x => x.EventId == eventEntity.Id && x.OrganizationId == organizationId, ct);
        if (hasActivities)
        {
            return;
        }

        foreach (var day in days)
        {
            db.EventActivities.Add(new EventActivityEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventEntity.Id,
                EventDayId = day.Id,
                Title = "Breakfast",
                Type = "Meal",
                StartTime = new TimeOnly(8, 30),
                EndTime = new TimeOnly(9, 15),
                LocationName = "Hotel Restaurant",
                Notes = "Breakfast buffet",
                CheckInEnabled = false,
                CheckInMode = "EntryOnly",
                MenuText = "Tea, coffee, eggs, fresh bread"
            });

            db.EventActivities.Add(new EventActivityEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventEntity.Id,
                EventDayId = day.Id,
                Title = "City walk",
                Type = "Other",
                StartTime = new TimeOnly(10, 30),
                EndTime = new TimeOnly(12, 0),
                LocationName = "Main square",
                Address = "Old Town",
                Directions = "Meet by the fountain",
                Notes = "Bring comfortable shoes",
                CheckInEnabled = false,
                CheckInMode = "EntryOnly"
            });

            db.EventActivities.Add(new EventActivityEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = eventEntity.Id,
                EventDayId = day.Id,
                Title = "Feedback",
                Type = "Other",
                StartTime = null,
                EndTime = null,
                Notes = "Share your thoughts",
                SurveyUrl = "https://example.com/tripflow/survey",
                CheckInEnabled = false,
                CheckInMode = "EntryOnly"
            });
        }

        state.Seeded = true;
    }

    private static string CreatePortalJson(string eventName, string time, string place, string mapsUrl)
    {
        var obj = new
        {
            meeting = new
            {
                time,
                place,
                mapsUrl,
                note = $"Welcome to {eventName}. Please arrive 15 minutes early."
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
