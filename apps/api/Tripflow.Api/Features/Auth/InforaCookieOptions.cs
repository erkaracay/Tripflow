using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Tripflow.Api.Features.Auth;

/// <summary>
/// Config for auth and portal session cookies (shared domain so apex + www work).
/// </summary>
public sealed record InforaCookieOptions(
    string? Domain,
    bool Secure,
    SameSiteMode SameSite)
{
    public const string AuthCookieName = "infora_token";
    public const string PortalCookieName = "infora_portal_session";

    public static InforaCookieOptions FromConfiguration(IConfiguration configuration)
    {
        var domain = configuration["Cookie:Domain"]?.Trim();
        var sameSiteRaw = configuration["Cookie:SameSite"]?.Trim();
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        var secure = ResolveSecure(configuration["Cookie:Secure"], isDevelopment);
        var sameSite = ParseSameSite(sameSiteRaw);

        if (sameSite == SameSiteMode.None && !secure)
        {
            throw new InvalidOperationException("Cookie__SameSite=None requires Cookie__Secure=true.");
        }

        return new InforaCookieOptions(string.IsNullOrEmpty(domain) ? null : domain, secure, sameSite);
    }

    private static bool ResolveSecure(string? secureRaw, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(secureRaw))
        {
            return !isDevelopment;
        }

        if (!bool.TryParse(secureRaw.Trim(), out var secure))
        {
            throw new InvalidOperationException("Cookie__Secure must be set to 'true' or 'false' when provided.");
        }

        if (!isDevelopment && !secure)
        {
            throw new InvalidOperationException("Cookie__Secure=false is only supported when ASPNETCORE_ENVIRONMENT=Development.");
        }

        return secure;
    }

    private static SameSiteMode ParseSameSite(string? sameSiteRaw)
        => sameSiteRaw?.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            _ => SameSiteMode.Lax
        };

    public CookieOptions BuildCookieOptions(TimeSpan? maxAge = null, DateTime? expires = null)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = Secure,
            SameSite = SameSite,
            Path = "/"
        };
        if (!string.IsNullOrEmpty(Domain))
            opts.Domain = Domain;
        if (maxAge.HasValue)
            opts.MaxAge = maxAge;
        if (expires.HasValue)
            opts.Expires = expires.Value;
        return opts;
    }

    public CookieOptions BuildClearCookieOptions()
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = Secure,
            SameSite = SameSite,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch
        };
        if (!string.IsNullOrEmpty(Domain))
            opts.Domain = Domain;
        return opts;
    }
}
