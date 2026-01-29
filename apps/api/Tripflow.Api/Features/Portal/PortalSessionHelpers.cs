using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Portal;

internal static class PortalSessionHelpers
{
    internal static string CreateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    internal static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    internal static async Task<PortalSessionEntity?> GetValidSessionAsync(
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var token = httpContext.Request.Headers["X-Portal-Session"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        var session = await db.PortalSessions
            .Include(x => x.Participant)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.ExpiresAt > now, ct);

        return session;
    }

    internal static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
