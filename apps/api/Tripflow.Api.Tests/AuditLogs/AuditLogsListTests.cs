using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.AuditLogs;
using Tripflow.Api.Tests.Infrastructure;

namespace Tripflow.Api.Tests.AuditLogs;

public sealed class AuditLogsListTests : IntegrationTestBase
{
    public AuditLogsListTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAuditLogs_Unauthenticated_Returns401()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLogs_AgencyAdmin_ReturnsOnlyOwnOrganization()
    {
        var orgA = await TestSeed.CreateOrganizationAsync(Factory, slug: $"audit-a-{Guid.NewGuid():n}");
        var orgB = await TestSeed.CreateOrganizationAsync(Factory, slug: $"audit-b-{Guid.NewGuid():n}");
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"agency-audit-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            role: "AgencyAdmin",
            organizationId: orgA.Id,
            fullName: "Agency Audit");

        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UserId = user.Id,
            OrganizationId = orgA.Id,
            Role = "AgencyAdmin",
            Action = "event.update",
            TargetType = "event",
            TargetId = Guid.NewGuid().ToString(),
            Result = "success",
            ExtraJson = """{"changedFields":["name"]}"""
        });
        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-4),
            OrganizationId = orgB.Id,
            Role = "AgencyAdmin",
            Action = "event.update",
            TargetType = "event",
            TargetId = Guid.NewGuid().ToString(),
            Result = "success"
        });

        var token = JwtTestTokenFactory.Create(GetJwtOptions(), user.Id, "AgencyAdmin", organizationId: orgA.Id, email: user.Email, fullName: user.FullName);
        using var client = CreateClient().WithBearer(token);

        var response = await client.GetFromJsonAsync<AuditLogListResponse>("/api/audit-logs?pageSize=10");

        response.Should().NotBeNull();
        response!.Total.Should().Be(1);
        response.Items.Should().ContainSingle();
        response.Items[0].OrganizationId.Should().Be(orgA.Id);
        response.Items[0].UserEmail.Should().Be(user.Email);
        response.Items[0].UserFullName.Should().Be(user.FullName);
    }

    [Fact]
    public async Task GetAuditLogs_SuperAdmin_WithOrgScope_ReturnsSelectedOrgAndGlobal()
    {
        var orgA = await TestSeed.CreateOrganizationAsync(Factory, slug: $"audit-super-a-{Guid.NewGuid():n}");
        var orgB = await TestSeed.CreateOrganizationAsync(Factory, slug: $"audit-super-b-{Guid.NewGuid():n}");
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"super-audit-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            role: "SuperAdmin",
            organizationId: null,
            fullName: "Super Audit");

        var selectedTargetId = Guid.NewGuid().ToString();
        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            UserId = user.Id,
            OrganizationId = orgA.Id,
            Role = "SuperAdmin",
            Action = "event.archive",
            TargetType = "event",
            TargetId = selectedTargetId,
            Result = "success"
        });
        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            UserId = user.Id,
            OrganizationId = null,
            Role = "SuperAdmin",
            Action = "auth.logout",
            TargetType = "user",
            TargetId = user.Id.ToString(),
            Result = "success"
        });
        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            UserId = user.Id,
            OrganizationId = orgB.Id,
            Role = "SuperAdmin",
            Action = "event.archive",
            TargetType = "event",
            TargetId = Guid.NewGuid().ToString(),
            Result = "success"
        });

        var token = JwtTestTokenFactory.Create(GetJwtOptions(), user.Id, "SuperAdmin", email: user.Email, fullName: user.FullName);
        using var client = CreateClient().WithBearer(token).WithOrgScope(orgA.Id);

        var response = await client.GetFromJsonAsync<AuditLogListResponse>("/api/audit-logs?pageSize=10");

        response.Should().NotBeNull();
        response!.Total.Should().Be(2);
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(x => x.OrganizationId == orgA.Id && x.TargetId == selectedTargetId);
        response.Items.Should().Contain(x => x.OrganizationId == null && x.Action == "auth.logout");
        response.Items.Should().NotContain(x => x.OrganizationId == orgB.Id);
    }

    [Fact]
    public async Task GetAuditLogs_ActionPrefixFilter_ReturnsMatchingRows()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory, slug: $"audit-action-{Guid.NewGuid():n}");
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"audit-action-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            role: "AgencyAdmin",
            organizationId: org.Id,
            fullName: "Audit Action");

        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = "AgencyAdmin",
            Action = "auth.login",
            TargetType = "user",
            TargetId = user.Id.ToString(),
            Result = "fail",
            ExtraJson = """{"reason":"invalid_credentials"}"""
        });
        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = "AgencyAdmin",
            Action = "event.update",
            TargetType = "event",
            TargetId = Guid.NewGuid().ToString(),
            Result = "success"
        });

        var token = JwtTestTokenFactory.Create(GetJwtOptions(), user.Id, "AgencyAdmin", organizationId: org.Id, email: user.Email, fullName: user.FullName);
        using var client = CreateClient().WithBearer(token);

        var response = await client.GetFromJsonAsync<AuditLogListResponse>("/api/audit-logs?action=auth.login&result=fail&pageSize=10");

        response.Should().NotBeNull();
        response!.Total.Should().Be(1);
        response.Items.Should().ContainSingle(x => x.Action == "auth.login" && x.Result == "fail");
    }

    [Fact]
    public async Task GetAuditLogs_DateRange_FiltersInclusively()
    {
        var org = await TestSeed.CreateOrganizationAsync(Factory, slug: $"audit-date-{Guid.NewGuid():n}");
        var user = await TestSeed.CreateUserAsync(
            Factory,
            $"audit-date-{Guid.NewGuid():n}@test.local",
            "Passw0rd!",
            role: "AgencyAdmin",
            organizationId: org.Id,
            fullName: "Audit Date");

        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = new DateTime(2026, 4, 10, 10, 15, 0, DateTimeKind.Utc),
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = "AgencyAdmin",
            Action = "participant.import",
            TargetType = "event",
            TargetId = Guid.NewGuid().ToString(),
            Result = "success"
        });
        await SeedAuditLogAsync(new AuditLogEntity
        {
            CreatedAt = new DateTime(2026, 4, 11, 8, 30, 0, DateTimeKind.Utc),
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = "AgencyAdmin",
            Action = "participant.import",
            TargetType = "event",
            TargetId = Guid.NewGuid().ToString(),
            Result = "success"
        });

        var token = JwtTestTokenFactory.Create(GetJwtOptions(), user.Id, "AgencyAdmin", organizationId: org.Id, email: user.Email, fullName: user.FullName);
        using var client = CreateClient().WithBearer(token);

        var response = await client.GetFromJsonAsync<AuditLogListResponse>(
            "/api/audit-logs?action=participant.import&from=2026-04-11&to=2026-04-11&pageSize=10");

        response.Should().NotBeNull();
        response!.Total.Should().Be(1);
        response.Items.Should().ContainSingle();
        response.Items[0].CreatedAt.Should().Be(new DateTime(2026, 4, 11, 8, 30, 0, DateTimeKind.Utc));
    }

    private async Task SeedAuditLogAsync(AuditLogEntity entity, CancellationToken ct = default)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        db.AuditLogs.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
