using System.Net;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.CrossCutting;

public sealed class ProblemDetailsTests : IntegrationTestBase
{
    public ProblemDetailsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UnhandledException_Returns500_WithProblemJsonContentType_AndCorrelationIdExtension()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/_dev/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var json = await response.ReadJsonAsync();
        json.GetProperty("status").GetInt32().Should().Be(500);
        json.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
