using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Dev;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Auth;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Features.Portal;
using Tripflow.Api.Features.Tours;
using Tripflow.Api.Features.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    };

    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("TripflowDb") ?? builder.Configuration["CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string missing. Set 'ConnectionStrings:TripflowDb' via user-secrets or set CONNECTION_STRING env var.");
}

builder.Services.AddDbContext<TripflowDbContext>(opt => opt.UseNpgsql(connectionString));

var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);

builder.Services.AddScoped<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("SuperAdmin", "AgencyAdmin"));
    options.AddPolicy("GuideOnly", policy => policy.RequireRole("Guide"));
});

// CORS
const string WebCorsPolicy = "WebCors";
var allowedOriginsValue = builder.Configuration["CORS_ALLOWED_ORIGINS"];
var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
if (!string.IsNullOrWhiteSpace(allowedOriginsValue))
{
    foreach (var origin in allowedOriginsValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        allowedOrigins.Add(origin.TrimEnd('/'));
    }
}
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                var normalizedOrigin = origin.TrimEnd('/');
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
                {
                    return allowedOrigins.Contains(normalizedOrigin);
                }

                if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return allowedOrigins.Contains(normalizedOrigin);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
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
app.UseAuthentication();
app.UseAuthorization();

// Health
app.MapGet("/health", async (TripflowDbContext db) =>
{
    var dbStatus = "ok";
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
    }
    catch
    {
        dbStatus = "error";
    }

    return Results.Ok(new { status = "ok", db = dbStatus });
})
   .WithName("Health")
   .WithOpenApi();

app.MapGet("/version", () =>
{
    var version = app.Configuration["APP_VERSION"] ?? "dev";
    return Results.Ok(new { version, timestamp = DateTime.UtcNow });
})
   .WithName("Version")
   .WithOpenApi();

app.MapAuthEndpoints();
app.MapOrganizationEndpoints();
app.MapGuideEndpoints();
app.MapUsersEndpoints();
app.MapToursEndpoints();
app.MapPortalAccessEndpoints();

app.Run();
