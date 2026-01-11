using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Tripflow.Api.Data;

public sealed class TripflowDbContextFactory : IDesignTimeDbContextFactory<TripflowDbContext>
{
    public TripflowDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("TripflowDb")
            ?? configuration["CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string missing. Set user-secrets key 'ConnectionStrings:TripflowDb'.");
        }

        var options = new DbContextOptionsBuilder<TripflowDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TripflowDbContext(options);
    }
}
