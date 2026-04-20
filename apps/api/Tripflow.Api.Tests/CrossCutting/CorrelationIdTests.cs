using System.Net;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.CrossCutting;

public sealed class CorrelationIdTests : IntegrationTestBase
{
    private const string HeaderName = "X-Correlation-Id";

    public CorrelationIdTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Response_IncludesXCorrelationIdHeader_WhenClientOmitsIt()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidXCorrelationId_FromClient_IsEchoed()
    {
        const string custom = "corr-abc-123";
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(HeaderName, custom);

        var response = await client.SendAsync(request);
        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue();
        values!.Single().Should().Be(custom);
    }
}
