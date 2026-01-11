using Tripflow.Api.Features.Tours;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Dev;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("TripflowDb") ?? builder.Configuration["CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string missing. Set 'ConnectionStrings:TripflowDb' via user-secrets or set CONNECTION_STRING env var.");
}

builder.Services.AddDbContext<TripflowDbContext>(opt => opt.UseNpgsql(connectionString));

// CORS
const string WebCorsPolicy = "WebCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapPost("/api/dev/seed", async (TripflowDbContext db, CancellationToken ct) =>
    {
        var (seeded, message) = await DevSeed.SeedAsync(db, ct);
        return Results.Ok(new { seeded, message });
    })
    .WithTags("Dev")
    .WithSummary("Dev seed (Development only)")
    .WithDescription("Seeds demo data for local development.")
    .WithOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(WebCorsPolicy);

// Health
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .WithOpenApi();

app.MapToursEndpoints();

app.Run();
