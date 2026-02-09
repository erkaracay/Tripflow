using Microsoft.AspNetCore.Http;

namespace Tripflow.Api.Helpers;

/// <summary>
/// Helper to resolve the public base URL for frontend links (QR codes, portal links, etc.).
/// </summary>
internal static class PublicUrlHelper
{
    /// <summary>
    /// Gets the public base URL from HttpContext.
    /// Checks X-Forwarded-Proto/Host headers first (for reverse proxies),
    /// then falls back to Request.Scheme/Host.
    /// </summary>
    internal static string GetPublicBaseUrl(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;

        // Ensure scheme is https if forwarded proto is https
        if (request.Headers["X-Forwarded-Proto"].FirstOrDefault() == "https")
        {
            scheme = "https";
        }

        return $"{scheme}://{host}";
    }
}
