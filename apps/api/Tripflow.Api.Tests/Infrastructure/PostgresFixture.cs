using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Tripflow.Api.Data;

namespace Tripflow.Api.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("tripflow_test")
        .WithUsername("tripflow")
        .WithPassword("tripflow")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<TripflowDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var ctx = new TripflowDbContext(options);
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
