using System.Net;
using Tripflow.Api.Features.Portal;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Portal;

public sealed class PortalRateLimitTests : IntegrationTestBase
{
    public PortalRateLimitTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Portal_FifteenFailures_ThenBlocked_Returns429_WithRetryAfter()
    {
        var code = ("RL" + Random.Shared.Next(10_000, 99_999)).ToUpperInvariant();

        using var client = CreateClient();
        for (var i = 0; i < 15; i++)
        {
            var failed = await client.PostJsonAsync("/api/portal/login",
                new PortalLoginRequest(code, new string('1', 11)));
            failed.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"attempt #{i + 1} should be a 400 (unknown code)");
        }

        var blocked = await client.PostJsonAsync("/api/portal/login",
            new PortalLoginRequest(code, new string('1', 11)));
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.Contains("Retry-After").Should().BeTrue();

        var body = await blocked.ReadJsonAsync();
        body.GetProperty("code").GetString().Should().Be("rate_limited");
    }
}
