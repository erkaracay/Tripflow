using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Tests.Infrastructure;

/// <summary>
/// Helpers that seed minimal rows into the test DB via the API's own services.
/// Resolves <see cref="IPasswordHasher{TUser}"/> from DI so hashes match what
/// the login handler will verify.
/// </summary>
public static class TestSeed
{
    public static async Task<OrganizationEntity> CreateOrganizationAsync(
        TripflowApiFactory factory,
        string? slug = null,
        CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var now = DateTime.UtcNow;
        var org = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Test Org {Guid.NewGuid():N}",
            Slug = slug ?? $"test-{Guid.NewGuid():N}",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);
        return org;
    }

    public static async Task<UserEntity> CreateUserAsync(
        TripflowApiFactory factory,
        string email,
        string password,
        string role = "AgencyAdmin",
        Guid? organizationId = null,
        string? fullName = null,
        CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserEntity>>();

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName,
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public static async Task<EventEntity> CreateEventAsync(
        TripflowApiFactory factory,
        Guid organizationId,
        string accessCode,
        string? name = null,
        CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var ev = new EventEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name ?? $"Test Event {accessCode}",
            StartDate = start,
            EndDate = start.AddDays(3),
            EventAccessCode = accessCode,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);
        return ev;
    }

    public static async Task<EventDocTabEntity> CreateEventDocTabAsync(
        TripflowApiFactory factory,
        EventEntity eventEntity,
        string title,
        string type = "Hotel",
        int sortOrder = 1,
        CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var tab = new EventDocTabEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = eventEntity.OrganizationId,
            EventId = eventEntity.Id,
            Title = title,
            Type = type,
            SortOrder = sortOrder,
            IsActive = true,
            ContentJson = "{}",
            CreatedAt = DateTime.UtcNow,
        };

        db.EventDocTabs.Add(tab);
        await db.SaveChangesAsync(ct);
        return tab;
    }

    public static async Task<ParticipantEntity> CreateParticipantAsync(
        TripflowApiFactory factory,
        EventEntity eventEntity,
        string tcNo,
        string? fullName = null,
        CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var first = "Test";
        var last = $"Participant-{tcNo[^4..]}";
        var participant = new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            OrganizationId = eventEntity.OrganizationId,
            FirstName = first,
            LastName = last,
            FullName = fullName ?? $"{first} {last}",
            Phone = "+905550000000",
            TcNo = tcNo,
            BirthDate = new DateOnly(1990, 1, 1),
            Gender = ParticipantGender.Other,
            CheckInCode = Guid.NewGuid().ToString("n"),
            CreatedAt = DateTime.UtcNow,
        };

        db.Participants.Add(participant);
        await db.SaveChangesAsync(ct);
        return participant;
    }
}
