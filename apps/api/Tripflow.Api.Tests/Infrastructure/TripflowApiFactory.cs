using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CONNECTION_STRING"] = _connectionString,
                ["ConnectionStrings:TripflowDb"] = _connectionString,
                ["JWT_ISSUER"] = "tripflow-tests",
                ["JWT_AUDIENCE"] = "tripflow-tests",
                ["JWT_SECRET"] = "tripflow-tests-secret-key-must-be-at-least-32-characters-long",
                ["Cookie:Secure"] = "false",
                ["Cookie:SameSite"] = "Lax",
            });
        });
    }
}
