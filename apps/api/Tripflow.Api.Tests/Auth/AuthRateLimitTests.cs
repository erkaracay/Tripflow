using System.Net;
using Tripflow.Api.Features.Auth;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Auth;

public sealed class AuthRateLimitTests : IntegrationTestBase
{
    public AuthRateLimitTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_FifteenFailures_SixteenthReturns429_WithRetryAfterHeader_AndCodeBody()
    {
        var email = $"ratelimit-{Guid.NewGuid():n}@test.local";
        await TestSeed.CreateUserAsync(Factory, email, "Passw0rd!");

        using var client = CreateClient();
        for (var i = 0; i < 15; i++)
        {
            var failed = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "WrongOne1!"));
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"failure #{i + 1} should still be 401");
        }

        var blocked = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "WrongOne1!"));
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.Contains("Retry-After").Should().BeTrue();

        var body = await blocked.ReadJsonAsync();
        body.GetProperty("code").GetString().Should().Be("rate_limited");
        body.GetProperty("retryAfterSeconds").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_SuccessAfterFailures_ClearsCounter()
    {
        var email = $"ratelimit-clear-{Guid.NewGuid():n}@test.local";
        await TestSeed.CreateUserAsync(Factory, email, "Passw0rd!");

        using var client = CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var failed = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "WrongOne1!"));
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var success = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!"));
        success.StatusCode.Should().Be(HttpStatusCode.OK);

        for (var i = 0; i < 5; i++)
        {
            var failed = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "WrongOne1!"));
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "counter should have been cleared by the success");
        }
    }
}
