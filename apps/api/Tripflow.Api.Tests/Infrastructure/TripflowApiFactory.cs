using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tripflow.Api.Data;

namespace Tripflow.Api.Tests.Infrastructure;

public sealed class TripflowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TripflowApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads CONNECTION_STRING and JWT config before builder.Build() is called,
        // so ConfigureAppConfiguration cannot override those eager reads.
        // The env vars (set by CI or the developer's user-secrets) provide the startup values;
        // ConfigureTestServices below replaces the DbContext with the Testcontainer connection.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cookie:Secure"] = "false",
                ["Cookie:SameSite"] = "Lax",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace the DbContext registrations wired to the startup connection string
            // (from env var / user-secrets) with the isolated Testcontainer database.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<TripflowDbContext>)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextFactory<>)
                             && d.ServiceType.GetGenericArguments()[0] == typeof(TripflowDbContext)))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            Action<DbContextOptionsBuilder> testDb = o => o.UseNpgsql(_connectionString);
            services.AddDbContext<TripflowDbContext>(testDb);
            services.AddDbContextFactory<TripflowDbContext>(testDb, ServiceLifetime.Scoped);
        });
    }
}
