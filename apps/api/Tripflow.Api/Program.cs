using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Dev;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Auth;
using Tripflow.Api.Features.Dev;
using Tripflow.Api.Features.Organizations;
using Tripflow.Api.Features.Portal;
using Tripflow.Api.Features.Events;
using Tripflow.Api.Features.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
if (builder.Environment.IsDevelopment())
{
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
}

var connectionString = builder.Configuration.GetConnectionString("TripflowDb") ?? builder.Configuration["CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string missing. Set 'ConnectionStrings:TripflowDb' via user-secrets or set CONNECTION_STRING env var.");
}

builder.Services.AddDbContext<TripflowDbContext>(opt => opt.UseNpgsql(connectionString));

var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);

var cookieOptions = InforaCookieOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(cookieOptions);

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
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var tokenFromHeader = ctx.Request.Headers.Authorization.FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(tokenFromHeader) && tokenFromHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Token = tokenFromHeader["Bearer ".Length..].Trim();
                    return Task.CompletedTask;
                }
                var tokenFromCookie = ctx.Request.Cookies[InforaCookieOptions.AuthCookieName];
                if (!string.IsNullOrWhiteSpace(tokenFromCookie))
                    ctx.Token = tokenFromCookie.Trim();
                return Task.CompletedTask;
            }
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
                    return false;

                // Localhost bypass only in Development — never in Production/Staging.
                if (builder.Environment.IsDevelopment()
                    && string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                return allowedOrigins.Contains(normalizedOrigin);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("X-Warning", "X-Tripflow-Warn");
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust only explicitly configured proxies / networks.
    // Env var form: ReverseProxy__KnownProxies="10.0.0.1,10.0.0.2"
    //               ReverseProxy__KnownNetworks="10.0.0.0/8"
    var knownProxiesRaw = builder.Configuration["ReverseProxy:KnownProxies"];
    if (!string.IsNullOrWhiteSpace(knownProxiesRaw))
    {
        foreach (var ip in knownProxiesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.Net.IPAddress.TryParse(ip, out var parsed))
                options.KnownProxies.Add(parsed);
        }
    }

    var knownNetworksRaw = builder.Configuration["ReverseProxy:KnownNetworks"];
    if (!string.IsNullOrWhiteSpace(knownNetworksRaw))
    {
        foreach (var cidr in knownNetworksRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = cidr.Split('/');
            if (parts.Length == 2
                && System.Net.IPAddress.TryParse(parts[0], out var prefix)
                && int.TryParse(parts[1], out var prefixLen))
            {
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLen));
            }
        }
    }
});

var app = builder.Build();
var isRender = !string.IsNullOrWhiteSpace(app.Configuration["RENDER"]);

// Swagger — Development only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/dev/seed", async (HttpContext httpContext, TripflowDbContext db, CancellationToken ct) =>
    {
        // Second guard: only accept loopback connections even in Development.
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !System.Net.IPAddress.IsLoopback(remoteIp))
            return Results.NotFound();

        var (seeded, message) = await DevSeed.SeedAsync(db, ct);
        return Results.Ok(new { seeded, message });
    })
    .WithTags("Dev")
    .WithSummary("Dev seed (Development only)")
    .WithDescription("Seeds demo data for local development.")
    .WithOpenApi();

    var devAdmin = app.MapGroup("/api/dev")
        .WithTags("Dev")
        .RequireAuthorization("AdminOnly");

    devAdmin.MapGet("/tools", DevToolsHandlers.GetTools)
        .WithSummary("Development tools capabilities")
        .WithDescription("Returns which development-only tools are enabled for the current environment.")
        .WithOpenApi();

    devAdmin.MapPost("/scenario-events", DevToolsHandlers.CreateScenarioEvent)
        .WithSummary("Create scenario event")
        .WithDescription("Creates a single development scenario event with generated schedule, participants, equipment, and optional meals/flights.")
        .WithOpenApi();

    devAdmin.MapDelete("/scenario-events/{eventId}", DevToolsHandlers.DeleteScenarioEvent)
        .WithSummary("Delete scenario event")
        .WithDescription("Deletes a generated development scenario event in the current organization.")
        .WithOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseHsts();
    if (!isRender)
    {
        app.UseHttpsRedirection();
    }
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
app.MapEventsEndpoints();
app.MapPortalLoginEndpoints();
app.MapPortalMealEndpoints();

app.Run();
