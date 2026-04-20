using System.Net;
using System.Net.Http.Json;
using Tripflow.Api.Features.Auth;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Auth;

public sealed class AuthLoginTests : IntegrationTestBase
{
    public AuthLoginTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithTokenAndCookie()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var email = $"login-valid-{Guid.NewGuid():n}@test.local";
        await TestSeed.CreateUserAsync(Factory, email, "Passw0rd!", role: "AgencyAdmin", organizationId: org.Id, fullName: "Admin One");

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Role.Should().Be("AgencyAdmin");

        response.GetSetCookie(InforaCookieOptions.AuthCookieName).Should().NotBeNull();
    }

    [Fact]
    public async Task Login_SetsInforaTokenCookie_WithExpectedFlags()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var email = $"login-cookie-{Guid.NewGuid():n}@test.local";
        await TestSeed.CreateUserAsync(Factory, email, "Passw0rd!", organizationId: org.Id);

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!"));

        var cookie = response.GetSetCookie(InforaCookieOptions.AuthCookieName);
        cookie.Should().NotBeNull();
        cookie!.Should().Contain("httponly", "auth cookie must be HttpOnly");
        cookie.Should().Contain("path=/", "auth cookie path must be /");
        cookie.Should().Contain("max-age=", "auth cookie must carry a lifetime");
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest($"unknown-{Guid.NewGuid():n}@test.local", "Whatever1!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401_WithIdenticalShape()
    {
        var email = $"login-wrong-{Guid.NewGuid():n}@test.local";
        await TestSeed.CreateUserAsync(Factory, email, "CorrectHorse1!");

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPass1!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain(email, "unknown-email vs wrong-password responses must not leak existence");
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/auth/login", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.ReadJsonAsync();
        json.GetProperty("message").GetString().Should().Be("Request body is required.");
    }

    [Fact]
    public async Task Login_MissingPassword_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest("user@test.local", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.ReadJsonAsync();
        json.GetProperty("message").GetString().Should().Be("Email and password are required.");
    }

    [Fact]
    public async Task Login_WritesSuccessAuditRow()
    {
        var email = $"login-audit-ok-{Guid.NewGuid():n}@test.local";
        var user = await TestSeed.CreateUserAsync(Factory, email, "Passw0rd!");

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var logs = await ReadAuditLogsAsync("auth.login");
        logs.Should().ContainSingle(l => l.Result == "success" && l.TargetId == user.Id.ToString());
    }

    [Fact]
    public async Task Login_FailedAttempt_WritesFailAuditRow()
    {
        var email = $"login-audit-fail-{Guid.NewGuid():n}@test.local";
        await TestSeed.CreateUserAsync(Factory, email, "Passw0rd!");

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "WrongGuess1!"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var logs = await ReadAuditLogsAsync("auth.login");
        logs.Should().Contain(l => l.Result == "fail"
            && l.ExtraJson != null
            && l.ExtraJson.Contains("invalid_credentials", StringComparison.Ordinal));
    }
}
