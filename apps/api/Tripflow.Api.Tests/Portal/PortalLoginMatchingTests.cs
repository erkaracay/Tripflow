using System.Net;
using Tripflow.Api.Features.Auth;
using Tripflow.Api.Features.Portal;
using Tripflow.Api.Helpers;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.Portal;

public sealed class PortalLoginMatchingTests : IntegrationTestBase
{
    public PortalLoginMatchingTests(PostgresFixture fixture) : base(fixture) { }

    private static string UniqueCode(string prefix)
    {
        var suffix = Random.Shared.Next(1000, 9999).ToString();
        return (prefix + suffix).ToUpperInvariant();
    }

    private static string UniqueTc()
    {
        // 11 digits; TC validity rules aren't enforced by the handler.
        var n = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        return n.ToString();
    }

    [Fact]
    public async Task Portal_UnknownAccessCode_Returns400_InvalidEventAccessCode()
    {
        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/portal/login",
            new PortalLoginRequest(UniqueCode("UNK"), UniqueTc()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.ReadJsonAsync();
        json.GetProperty("code").GetString().Should().Be("invalid_event_access_code");
    }

    [Fact]
    public async Task Portal_TcNoNotInEvent_Returns400_TcNoNotFound()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var code = UniqueCode("NONE");
        await TestSeed.CreateEventAsync(Factory, org.Id, code);

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/portal/login",
            new PortalLoginRequest(code, UniqueTc()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.ReadJsonAsync();
        json.GetProperty("code").GetString().Should().Be("tcno_not_found");
    }

    [Fact]
    public async Task Portal_ValidMatch_Returns200_WithSessionAndCookie()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var code = UniqueCode("OK");
        var ev = await TestSeed.CreateEventAsync(Factory, org.Id, code);
        var tc = UniqueTc();
        await TestSeed.CreateParticipantAsync(Factory, ev, tc);

        using var client = CreateClient();
        var response = await client.PostJsonAsync("/api/portal/login",
            new PortalLoginRequest(code, tc));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("portalSessionToken").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("eventId").GetGuid().Should().Be(ev.Id);

        response.GetSetCookie(InforaCookieOptions.PortalCookieName).Should().NotBeNull();
    }

    [Fact]
    public async Task Portal_LoginSuccess_AuditHasMaskedTcNo()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory);
        var code = UniqueCode("MSK");
        var ev = await TestSeed.CreateEventAsync(Factory, org.Id, code);
        var tc = UniqueTc();
        await TestSeed.CreateParticipantAsync(Factory, ev, tc);

        using var client = CreateClient();
        var ok = await client.PostJsonAsync("/api/portal/login", new PortalLoginRequest(code, tc));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // A failing attempt so we can see the masked TC in an audit row:
        var fail = await client.PostJsonAsync("/api/portal/login",
            new PortalLoginRequest(code, new string('1', 11)));
        fail.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var logs = await ReadAuditLogsAsync("portal.login");
        var masked = AuditLogHelpers.MaskTcNo(new string('1', 11));
        logs.Should().Contain(l => l.ExtraJson != null && l.ExtraJson.Contains(masked!, StringComparison.Ordinal));
        logs.Should().NotContain(l => l.ExtraJson != null && l.ExtraJson.Contains("\"" + new string('1', 11) + "\"", StringComparison.Ordinal));
    }
}
