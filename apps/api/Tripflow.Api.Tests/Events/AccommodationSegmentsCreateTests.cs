using System.Net;
using System.Net.Http.Json;
using Tripflow.Api.Features.Events;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Events;

public sealed class AccommodationSegmentsCreateTests : IntegrationTestBase
{
    public AccommodationSegmentsCreateTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateSegment_WithNonOverlappingDates_Returns201()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory, slug: $"segment-org-{Guid.NewGuid():n}");
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"segment-admin-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            role: "AgencyAdmin",
            organizationId: org.Id,
            fullName: "Segment Admin");
        var ev = await TestSeed.CreateEventAsync(Factory, org.Id, accessCode: $"SEG{Guid.NewGuid():N}"[..8], name: "Segment Event");
        var hotelTab = await TestSeed.CreateEventDocTabAsync(Factory, ev, title: "Mona Plaza");

        var token = JwtTestTokenFactory.Create(GetJwtOptions(), user.Id, "AgencyAdmin", organizationId: org.Id, email: user.Email, fullName: user.FullName);
        using var client = CreateClient().WithBearer(token);

        var response = await client.PostJsonAsync(
            $"/api/events/{ev.Id}/accommodation-segments",
            new UpsertAccommodationSegmentRequest(
                hotelTab.Id,
                ev.StartDate.ToString("yyyy-MM-dd"),
                ev.StartDate.AddDays(1).ToString("yyyy-MM-dd"),
                null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AccommodationSegmentDto>();
        body.Should().NotBeNull();
        body!.DefaultAccommodationDocTabId.Should().Be(hotelTab.Id);
        body.DefaultAccommodationTitle.Should().Be("Mona Plaza");
    }
}
