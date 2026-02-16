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
        var secureRaw = configuration["Cookie:Secure"];
        var secure = string.IsNullOrEmpty(secureRaw) || string.Equals(secureRaw, "true", StringComparison.OrdinalIgnoreCase);
        // In development (localhost), Secure cookies won't work over HTTP, so disable Secure
        var isDevelopment = string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(domain) || domain.Contains("localhost", StringComparison.OrdinalIgnoreCase);
        if (isDevelopment && secure)
        {
            secure = false;
        }
        var sameSiteRaw = configuration["Cookie:SameSite"]?.Trim();
        var sameSite = sameSiteRaw switch
        {
            "None" => SameSiteMode.None,
            "Strict" => SameSiteMode.Strict,
            _ => SameSiteMode.Lax
        };
        return new InforaCookieOptions(string.IsNullOrEmpty(domain) ? null : domain, secure, sameSite);
    }

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
