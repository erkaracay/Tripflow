using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tripflow.Api.Helpers;

/// <summary>
/// Helper to resolve the public base URL for frontend links (QR codes, portal links, etc.).
/// </summary>
internal static class PublicUrlHelper
{
    /// <summary>
    /// Gets the public base URL.
    /// In production, always set APP_PUBLIC_BASE_URL in the environment so the URL is
    /// derived from config rather than request headers.
    /// In development, falls back to Request.Scheme/Host (safe for local use).
    /// X-Forwarded-* headers are intentionally NOT read here; the ForwardedHeaders
    /// middleware updates Request.Scheme / Request.Host when a trusted proxy is configured.
    /// </summary>
    internal static string GetPublicBaseUrl(HttpContext httpContext)
    {
        var configuration = httpContext.RequestServices.GetService<IConfiguration>();
        var configured = configuration?["APP_PUBLIC_BASE_URL"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        // Development fallback: derive from the processed request context.
        var request = httpContext.Request;
        return $"{request.Scheme}://{request.Host.Value}";
    }
}
