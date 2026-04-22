using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Events;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Events;

public sealed class DeleteDocTabTests : IntegrationTestBase
{
    public DeleteDocTabTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DeleteDocTab_WhenHotelTabHasNoReferences_Returns204()
    {
        var ctx = await SeedAdminContextAsync("doctab-clean");
        var hotelTab = await TestSeed.CreateEventDocTabAsync(Factory, ctx.Event, title: "Lonely Hotel");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.DeleteAsync($"/api/events/{ctx.Event.Id}/docs/tabs/{hotelTab.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var stillExists = await db.EventDocTabs.AsNoTracking().AnyAsync(x => x.Id == hotelTab.Id);
        stillExists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDocTab_WhenHotelTabIsDefaultForSegments_Returns409WithSegments()
    {
        var ctx = await SeedAdminContextAsync("doctab-conflict");
        var hotelTab = await TestSeed.CreateEventDocTabAsync(Factory, ctx.Event, title: "Conflict Hotel");

        var segmentAId = await CreateSegmentAsync(
            ctx.Event,
            hotelTab.Id,
            startOffsetDays: 0,
            endOffsetDays: 1,
            sortOrder: 1);
        var segmentBId = await CreateSegmentAsync(
            ctx.Event,
            hotelTab.Id,
            startOffsetDays: 2,
            endOffsetDays: 3,
            sortOrder: 2);

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.DeleteAsync($"/api/events/{ctx.Event.Id}/docs/tabs/{hotelTab.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await response.ReadJsonAsync();
        payload.GetProperty("code").GetString().Should().Be("doc_tab_in_use_by_accommodation_segments");
        payload.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();

        var segments = payload.GetProperty("segments");
        segments.GetArrayLength().Should().Be(2);

        var returnedIds = new[]
        {
            segments[0].GetProperty("id").GetGuid(),
            segments[1].GetProperty("id").GetGuid(),
        };
        returnedIds.Should().BeEquivalentTo(new[] { segmentAId, segmentBId });

        // Each returned segment has participant count zero and correctly formatted dates.
        foreach (var segment in segments.EnumerateArray())
        {
            segment.GetProperty("participantCount").GetInt32().Should().Be(0);
            segment.GetProperty("startDate").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
            segment.GetProperty("endDate").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
        }

        // Tab must remain in DB — delete must not have partially executed.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var stillExists = await db.EventDocTabs.AsNoTracking().AnyAsync(x => x.Id == hotelTab.Id);
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDocTab_WhenOnlyReferencedByOverrideAssignments_ClearsOverridesAndReturns204()
    {
        var ctx = await SeedAdminContextAsync("doctab-override");
        var defaultHotel = await TestSeed.CreateEventDocTabAsync(Factory, ctx.Event, title: "Default Hotel", sortOrder: 1);
        var overrideHotel = await TestSeed.CreateEventDocTabAsync(Factory, ctx.Event, title: "Override Hotel", sortOrder: 2);

        var segmentId = await CreateSegmentAsync(
            ctx.Event,
            defaultHotel.Id,
            startOffsetDays: 0,
            endOffsetDays: 2,
            sortOrder: 1);

        var participant = await TestSeed.CreateParticipantAsync(
            Factory,
            ctx.Event,
            tcNo: $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}");

        var assignmentId = await CreateOverrideAssignmentAsync(
            ctx.Event,
            segmentId,
            participant.Id,
            overrideHotel.Id);

        using var client = CreateClient().WithBearer(ctx.Token);

        // Delete the hotel tab that is only referenced as an override — should succeed
        // because no segment uses it as default. Override must be cleared atomically.
        var response = await client.DeleteAsync($"/api/events/{ctx.Event.Id}/docs/tabs/{overrideHotel.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var tabStillExists = await db.EventDocTabs.AsNoTracking().AnyAsync(x => x.Id == overrideHotel.Id);
        tabStillExists.Should().BeFalse();

        var assignment = await db.ParticipantAccommodationAssignments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == assignmentId);
        assignment.Should().NotBeNull();
        assignment!.OverrideAccommodationDocTabId.Should().BeNull();

        var segmentStillExists = await db.EventAccommodationSegments.AsNoTracking().AnyAsync(x => x.Id == segmentId);
        segmentStillExists.Should().BeTrue();
    }

    private async Task<AdminContext> SeedAdminContextAsync(string slugPrefix)
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory, slug: $"{slugPrefix}-org-{Guid.NewGuid():n}");
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"{slugPrefix}-admin-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            role: "AgencyAdmin",
            organizationId: org.Id,
            fullName: "Doc Tab Admin");
        var ev = await TestSeed.CreateEventAsync(
            Factory,
            org.Id,
            accessCode: $"DOC{Guid.NewGuid():N}"[..8],
            name: $"{slugPrefix}-event");

        var token = JwtTestTokenFactory.Create(
            GetJwtOptions(),
            user.Id,
            "AgencyAdmin",
            organizationId: org.Id,
            email: user.Email,
            fullName: user.FullName);

        return new AdminContext(org, user, ev, token);
    }

    private async Task<Guid> CreateSegmentAsync(
        EventEntity eventEntity,
        Guid defaultDocTabId,
        int startOffsetDays,
        int endOffsetDays,
        int sortOrder,
        CancellationToken ct = default)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var now = DateTime.UtcNow;
        var segment = new EventAccommodationSegmentEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = eventEntity.OrganizationId,
            EventId = eventEntity.Id,
            DefaultAccommodationDocTabId = defaultDocTabId,
            StartDate = eventEntity.StartDate.AddDays(startOffsetDays),
            EndDate = eventEntity.StartDate.AddDays(endOffsetDays),
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.EventAccommodationSegments.Add(segment);
        await db.SaveChangesAsync(ct);
        return segment.Id;
    }

    private async Task<Guid> CreateOverrideAssignmentAsync(
        EventEntity eventEntity,
        Guid segmentId,
        Guid participantId,
        Guid overrideDocTabId,
        CancellationToken ct = default)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var now = DateTime.UtcNow;
        var assignment = new ParticipantAccommodationAssignmentEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = eventEntity.OrganizationId,
            EventId = eventEntity.Id,
            ParticipantId = participantId,
            SegmentId = segmentId,
            OverrideAccommodationDocTabId = overrideDocTabId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ParticipantAccommodationAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment.Id;
    }

    private sealed record AdminContext(
        OrganizationEntity Organization,
        UserEntity User,
        EventEntity Event,
        string Token);
}
