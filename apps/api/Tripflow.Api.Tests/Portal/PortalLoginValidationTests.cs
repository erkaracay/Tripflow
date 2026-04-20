using System.Net;
using Tripflow.Api.Features.Portal;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Portal;

public sealed class PortalLoginValidationTests : IntegrationTestBase
{
    public PortalLoginValidationTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Portal_EmptyBody_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/portal/login",
            new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.ReadJsonAsync();
        json.GetProperty("message").GetString().Should().Be("Request body is required.");
    }

    [Fact]
    public async Task Portal_InvalidTcNoFormat_Returns400_InvalidTcNoFormat()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var code = $"TCFMT{Random.Shared.Next(1000, 9999)}";
        await TestSeed.CreateEventAsync(Factory, org.Id, code);

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/portal/login",
            new PortalLoginRequest(code, "123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.ReadJsonAsync();
        json.GetProperty("code").GetString().Should().Be("invalid_tcno_format");
    }
}
