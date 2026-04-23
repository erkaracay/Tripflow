using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Events;

public sealed class TransferBulkEndpointsTests : IntegrationTestBase
{
    public TransferBulkEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task BulkApplyCommon_AppliesToEmptyDetailsOnly_InOnlyEmptyMode()
    {
        var ctx = await SeedAdminContextAsync("tr-common-empty");
        var filled = await SeedParticipantWithTransfer(
            ctx.Event,
            "12345678901",
            arrivalPickupPlace: "Existing Pickup",
            arrivalVehicle: "Existing Vehicle");
        var empty = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "12345678902");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/participants/transfers/bulk-apply-common",
            new
            {
                arrival = new
                {
                    pickupPlace = "Main Station",
                    vehicle = "Bus",
                    pickupTime = "07:30",
                },
                scope = "all",
                overwriteMode = "only_empty",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("affectedCount").GetInt32().Should().Be(2);
        payload.GetProperty("skippedCount").GetInt32().Should().Be(0);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var filledReloaded = await db.ParticipantDetails.AsNoTracking().SingleAsync(x => x.ParticipantId == filled.Id);
        filledReloaded.ArrivalTransferPickupPlace.Should().Be("Existing Pickup");
        filledReloaded.ArrivalTransferVehicle.Should().Be("Existing Vehicle");
        filledReloaded.ArrivalTransferPickupTime.Should().Be(new TimeOnly(7, 30));

        var emptyReloaded = await db.ParticipantDetails.AsNoTracking().SingleAsync(x => x.ParticipantId == empty.Id);
        emptyReloaded.ArrivalTransferPickupPlace.Should().Be("Main Station");
        emptyReloaded.ArrivalTransferVehicle.Should().Be("Bus");
        emptyReloaded.ArrivalTransferPickupTime.Should().Be(new TimeOnly(7, 30));
    }

    [Fact]
    public async Task BulkApplyCommon_OverwritesExisting_InOverwriteMode()
    {
        var ctx = await SeedAdminContextAsync("tr-common-over");
        var filled = await SeedParticipantWithTransfer(
            ctx.Event,
            "12345678901",
            arrivalPickupPlace: "Old",
            arrivalVehicle: "Old Bus");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/participants/transfers/bulk-apply-common",
            new
            {
                arrival = new
                {
                    pickupPlace = "New",
                    vehicle = "New Bus",
                },
                overwriteMode = "overwrite",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        var reloaded = await db.ParticipantDetails.AsNoTracking().SingleAsync(x => x.ParticipantId == filled.Id);
        reloaded.ArrivalTransferPickupPlace.Should().Be("New");
        reloaded.ArrivalTransferVehicle.Should().Be("New Bus");
    }

    [Fact]
    public async Task BulkApplyCommon_ReturnsBadRequest_WhenNoFields()
    {
        var ctx = await SeedAdminContextAsync("tr-common-nofields");
        await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "12345678901");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/participants/transfers/bulk-apply-common",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("code").GetString().Should().Be("no_fields_provided");
    }

    [Fact]
    public async Task BulkMatchSeats_AppliesSeatAndCompartmentOnlyToMatchingTcNos()
    {
        var ctx = await SeedAdminContextAsync("tr-seat-match");
        var p1 = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000001");
        var p2 = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "10000000002");

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/participants/transfers/bulk-match-seats",
            new
            {
                entries = new object[]
                {
                    new { tcNo = "10000000001", arrivalSeatNo = "14A", arrivalCompartmentNo = "2" },
                    new { tcNo = "10000000002", returnSeatNo = "7B" },
                    new { tcNo = "99999999999", arrivalSeatNo = "1" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedCount").GetInt32().Should().Be(2);
        var unmatched = payload.GetProperty("unmatchedTcNos").EnumerateArray().Select(x => x.GetString()).ToArray();
        unmatched.Should().BeEquivalentTo(new[] { "99999999999" });

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var d1 = await db.ParticipantDetails.AsNoTracking().SingleAsync(x => x.ParticipantId == p1.Id);
        d1.ArrivalTransferSeatNo.Should().Be("14A");
        d1.ArrivalTransferCompartmentNo.Should().Be("2");
        d1.ReturnTransferSeatNo.Should().BeNull();

        var d2 = await db.ParticipantDetails.AsNoTracking().SingleAsync(x => x.ParticipantId == p2.Id);
        d2.ArrivalTransferSeatNo.Should().BeNull();
        d2.ReturnTransferSeatNo.Should().Be("7B");
    }

    [Fact]
    public async Task BulkMatchSeats_ClearsFieldsWhenEmptyStringProvided()
    {
        var ctx = await SeedAdminContextAsync("tr-seat-clear");
        var participant = await TestSeed.CreateParticipantAsync(Factory, ctx.Event, "20000000001");

        using (var setupScope = Factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<TripflowDbContext>();
            setupDb.ParticipantDetails.Add(new ParticipantDetailsEntity
            {
                ParticipantId = participant.Id,
                ArrivalTransferSeatNo = "14A",
                ArrivalTransferCompartmentNo = "2",
                ReturnTransferSeatNo = "7B",
                ReturnTransferCompartmentNo = "3",
            });
            await setupDb.SaveChangesAsync();
        }

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ctx.Event.Id}/participants/transfers/bulk-match-seats",
            new
            {
                entries = new object[]
                {
                    new { tcNo = "20000000001", arrivalSeatNo = "", returnCompartmentNo = "" },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.ReadJsonAsync();
        payload.GetProperty("appliedCount").GetInt32().Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();

        var reloaded = await db.ParticipantDetails.AsNoTracking().SingleAsync(x => x.ParticipantId == participant.Id);
        reloaded.ArrivalTransferSeatNo.Should().BeNull();
        reloaded.ArrivalTransferCompartmentNo.Should().Be("2");
        reloaded.ReturnTransferSeatNo.Should().Be("7B");
        reloaded.ReturnTransferCompartmentNo.Should().BeNull();
    }

    [Fact]
    public async Task BulkApplyCommon_ReturnsNotFound_WhenEventBelongsToDifferentOrg()
    {
        var ctx = await SeedAdminContextAsync("tr-scope");
        var otherOrg = await TestSeed.CreateOrganizationAsync(Factory, slug: $"tr-scope-other-{Guid.NewGuid():n}");
        var otherEvent = await TestSeed.CreateEventAsync(Factory, otherOrg.Id, $"OTH{Guid.NewGuid():N}"[..8]);

        using var client = CreateClient().WithBearer(ctx.Token);

        var response = await client.PostJsonAsync(
            $"/api/events/{otherEvent.Id}/participants/transfers/bulk-apply-common",
            new { arrival = new { pickupPlace = "Main" } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<ParticipantEntity> SeedParticipantWithTransfer(
        EventEntity ev,
        string tcNo,
        string? arrivalPickupPlace = null,
        string? arrivalVehicle = null,
        CancellationToken ct = default)
    {
        var participant = await TestSeed.CreateParticipantAsync(Factory, ev, tcNo, ct: ct);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        db.ParticipantDetails.Add(new ParticipantDetailsEntity
        {
            ParticipantId = participant.Id,
            ArrivalTransferPickupPlace = arrivalPickupPlace,
            ArrivalTransferVehicle = arrivalVehicle,
        });
        await db.SaveChangesAsync(ct);
        return participant;
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
            fullName: "Transfer Admin");
        var ev = await TestSeed.CreateEventAsync(
            Factory,
            org.Id,
            accessCode: $"TRB{Guid.NewGuid():N}"[..8],
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
