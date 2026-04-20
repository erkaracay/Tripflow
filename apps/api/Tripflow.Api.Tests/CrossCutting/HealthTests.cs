using System.Net;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.CrossCutting;

public sealed class HealthTests : IntegrationTestBase
{
    public HealthTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Health_Returns200_WithStatusHealthy()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("status").GetString().Should().Be("healthy");
    }
}
