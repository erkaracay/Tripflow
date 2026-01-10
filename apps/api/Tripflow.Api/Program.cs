using Tripflow.Api.Features.Tours;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration["CONNECTION_STRING"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("CONNECTION_STRING missing. Export it in your shell.");
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
