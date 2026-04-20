using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;
using Tripflow.Api.Features.Auth;

namespace Tripflow.Api.Tests.Infrastructure;

[Collection("api")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    protected TripflowApiFactory Factory { get; private set; } = default!;

    protected IntegrationTestBase(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Factory = new TripflowApiFactory(_fixture.ConnectionString);
        _ = Factory.Services; // force host build

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
            await DatabaseResetter.ResetAsync(db);
        }

        RateLimiterReset.Clear();
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient()
    {
        return Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    protected JwtOptions GetJwtOptions()
    {
        return Factory.Services.GetRequiredService<JwtOptions>();
    }

    protected async Task<List<Data.Entities.AuditLogEntity>> ReadAuditLogsAsync(string action, CancellationToken ct = default)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripflowDbContext>();
        return await db.AuditLogs.AsNoTracking()
            .Where(x => x.Action == action)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }
}
