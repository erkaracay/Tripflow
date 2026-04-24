using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Events;

public sealed class FlightTicketBulkMatchTests : IntegrationTestBase
{
    public FlightTicketBulkMatchTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task BulkMatchTicket_AppliesToAllArrivalSegments_InOverwriteMode()
    {
        var ctx = await SeedAdminContextAsync("ft-overwrite");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000001");
        await SeedArrivalSegmentsAsync(ctx.Event, participant, new[] { "OLD-1", "OLD-2" });

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                overwriteMode = "overwrite",
                entries = new object[]
                {
                    new { tcNo = "10000000001", ticketNo = "NEW-123" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedParticipantCount").GetInt32().Should().Be(1);
        payload.GetProperty("appliedSegmentCount").GetInt32().Should().Be(2);
        payload.GetProperty("unmatchedTcNos").EnumerateArray().Should().BeEmpty();
        payload.GetProperty("noSegmentsTcNos").EnumerateArray().Should().BeEmpty();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var tickets = await db.ParticipantFlightSegments.AsNoTracking()
            .Where(x => x.ParticipantId == participant.Id && x.Direction == ParticipantFlightSegmentDirection.Arrival)
            .Select(x => x.TicketNo)
            .ToListAsync();
        tickets.Should().OnlyContain(t => t == "NEW-123");
        tickets.Should().HaveCount(2);
    }

    [Fact]
    public async Task BulkMatchTicket_OnlyEmptyMode_PreservesExistingTickets()
    {
        var ctx = await SeedAdminContextAsync("ft-onlyempty");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000002");
        await SeedArrivalSegmentsAsync(ctx.Event, participant, new[] { "EXISTING", null });

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                overwriteMode = "only_empty",
                entries = new object[]
                {
                    new { tcNo = "10000000002", ticketNo = "FILL-1" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedParticipantCount").GetInt32().Should().Be(1);
        payload.GetProperty("appliedSegmentCount").GetInt32().Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var tickets = await db.ParticipantFlightSegments.AsNoTracking()
            .Where(x => x.ParticipantId == participant.Id && x.Direction == ParticipantFlightSegmentDirection.Arrival)
            .OrderBy(x => x.SegmentIndex)
            .Select(x => x.TicketNo)
            .ToListAsync();
        tickets.Should().ContainInOrder("EXISTING", "FILL-1");
    }

    [Fact]
    public async Task BulkMatchTicket_DoesNotAffectOppositeDirection()
    {
        var ctx = await SeedAdminContextAsync("ft-direction");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000003");
        await SeedArrivalSegmentsAsync(ctx.Event, participant, new[] { (string?)null });
        await SeedReturnSegmentsAsync(ctx.Event, participant, new[] { "RETURN-KEEP" });

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                overwriteMode = "overwrite",
                entries = new object[]
                {
                    new { tcNo = "10000000003", ticketNo = "ARR-NEW" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var arrival = await db.ParticipantFlightSegments.AsNoTracking()
            .SingleAsync(x => x.ParticipantId == participant.Id && x.Direction == ParticipantFlightSegmentDirection.Arrival);
        var ret = await db.ParticipantFlightSegments.AsNoTracking()
            .SingleAsync(x => x.ParticipantId == participant.Id && x.Direction == ParticipantFlightSegmentDirection.Return);

        arrival.TicketNo.Should().Be("ARR-NEW");
        ret.TicketNo.Should().Be("RETURN-KEEP");
    }

    [Fact]
    public async Task BulkMatchTicket_ReturnsUnmatched_WhenTcNotInEvent()
    {
        var ctx = await SeedAdminContextAsync("ft-unmatched");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000004");
        await SeedArrivalSegmentsAsync(ctx.Event, participant, new[] { (string?)null });

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                entries = new object[]
                {
                    new { tcNo = "10000000004", ticketNo = "OK-1" },
                    new { tcNo = "99999999999", ticketNo = "NO-1" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedParticipantCount").GetInt32().Should().Be(1);
        var unmatched = payload.GetProperty("unmatchedTcNos").EnumerateArray().Select(x => x.GetString()).ToArray();
        unmatched.Should().BeEquivalentTo(new[] { "99999999999" });
    }

    [Fact]
    public async Task BulkMatchTicket_ReportsNoSegments_WhenParticipantHasNoSegmentsInDirection()
    {
        var ctx = await SeedAdminContextAsync("ft-nosegments");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000006");
        await SeedReturnSegmentsAsync(ctx.Event, participant, new[] { (string?)null });

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                entries = new object[]
                {
                    new { tcNo = "10000000006", ticketNo = "NO-SEG" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedParticipantCount").GetInt32().Should().Be(0);
        payload.GetProperty("appliedSegmentCount").GetInt32().Should().Be(0);
        payload.GetProperty("unmatchedTcNos").EnumerateArray().Should().BeEmpty();
        var noSegments = payload.GetProperty("noSegmentsTcNos").EnumerateArray().Select(x => x.GetString()).ToArray();
        noSegments.Should().BeEquivalentTo(new[] { "10000000006" });
    }

    [Fact]
    public async Task BulkMatchTicket_IgnoresInvalidTcAndEmptyTicket()
    {
        var ctx = await SeedAdminContextAsync("ft-invalid");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000007");
        await SeedArrivalSegmentsAsync(ctx.Event, participant, new[] { (string?)null });

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                entries = new object[]
                {
                    new { tcNo = "1234", ticketNo = "BAD-TC" },
                    new { tcNo = "10000000007", ticketNo = "" },
                    new { tcNo = "10000000007", ticketNo = "OK-1" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedParticipantCount").GetInt32().Should().Be(1);
        payload.GetProperty("appliedSegmentCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task BulkMatchTicket_ReturnsBadRequest_WhenDirectionMissing()
    {
        var ctx = await SeedAdminContextAsync("ft-nodir");
        await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000008");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                entries = new object[]
                {
                    new { tcNo = "10000000008", ticketNo = "X" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("code").GetString().Should().Be("invalid_direction");
    }

    [Fact]
    public async Task BulkMatchTicket_ReturnsBadRequest_WhenNoValidEntries()
    {
        var ctx = await SeedAdminContextAsync("ft-novalid");
        await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000009");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                entries = new object[]
                {
                    new { tcNo = "123", ticketNo = "X" },
                    new { tcNo = "10000000009", ticketNo = "" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("code").GetString().Should().Be("no_valid_entries");
    }

    [Fact]
    public async Task BulkMatchTicket_ReturnsNotFound_WhenEventBelongsToDifferentOrg()
    {
        var ctx = await SeedAdminContextAsync("ft-scope");
        var otherOrg = await TestSeed.CreateOrganizationAsync(Factory, slug: $"ft-scope-other-{Guid.NewGuid():n}");
        var otherEvent = await TestSeed.CreateEventAsync(Factory, otherOrg.Id, $"OTH{Guid.NewGuid():N}"[..8]);

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{otherEvent.Id}/flight-segments/bulk-match-ticket",
            new
            {
                direction = "Arrival",
                entries = new object[]
                {
                    new { tcNo = "10000000010", ticketNo = "X" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task SeedArrivalSegmentsAsync(EventEntity ev, ParticipantEntity participant, IEnumerable<string?> ticketNos)
        => SeedSegmentsAsync(ev, participant, ParticipantFlightSegmentDirection.Arrival, ticketNos);

    private Task SeedReturnSegmentsAsync(EventEntity ev, ParticipantEntity participant, IEnumerable<string?> ticketNos)
        => SeedSegmentsAsync(ev, participant, ParticipantFlightSegmentDirection.Return, ticketNos);

    private async Task SeedSegmentsAsync(
        EventEntity ev,
        ParticipantEntity participant,
        ParticipantFlightSegmentDirection direction,
        IEnumerable<string?> ticketNos)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var index = 1;
        foreach (var ticket in ticketNos)
        {
            db.ParticipantFlightSegments.Add(new ParticipantFlightSegmentEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = ev.OrganizationId,
                EventId = ev.Id,
                ParticipantId = participant.Id,
                Direction = direction,
                SegmentIndex = index++,
                FlightCode = $"TK{100 + index}",
                DepartureAirport = "IST",
                ArrivalAirport = "ESB",
                DepartureDate = new DateOnly(2026, 5, 1),
                DepartureTime = new TimeOnly(10, 0),
                TicketNo = ticket,
            });
        }
        await db.SaveChangesAsync();
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
            fullName: "Flight Ticket Admin");
        var ev = await TestSeed.CreateEventAsync(
            Factory,
            org.Id,
            accessCode: $"FTB{Guid.NewGuid():N}"[..8],
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

    private sealed record AdminContext(
        OrganizationEntity Organization,
        UserEntity User,
        EventEntity Event,
        string Token);
}
