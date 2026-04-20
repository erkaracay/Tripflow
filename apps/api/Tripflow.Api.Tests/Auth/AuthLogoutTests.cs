using System.Net;
using Tripflow.Api.Features.Auth;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Auth;

public sealed class AuthLogoutTests : IntegrationTestBase
{
    public AuthLogoutTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Logout_Returns200_AndExpiresCookie()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = response.GetSetCookie(InforaCookieOptions.AuthCookieName);
        cookie.Should().NotBeNull();
        cookie!.Should().Contain("expires=", "logout should expire the auth cookie");
    }

    [Fact]
    public async Task Logout_AuthenticatedUser_WritesAuditRow()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"logout-audit-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            organizationId: org.Id);
        var token = JwtTestTokenFactory.Create(GetJwtOptions(), user.Id, user.Role, user.OrganizationId, user.Email);

        using var client = CreateClient().WithBearer(token);
        var response = await client.PostAsync("/api/auth/logout", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var logs = await ReadAuditLogsAsync("auth.logout");
        logs.Should().ContainSingle(l => l.Result == "success" && l.TargetId == user.Id.ToString());
    }
}
