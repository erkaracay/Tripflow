using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Tripflow.Api.Features.Auth;

namespace Tripflow.Api.Tests.Infrastructure;

/// <summary>
/// Builds JWTs directly from <see cref="JwtOptions"/> so authenticated
/// endpoint tests don't depend on the login handler (which is itself
/// rate-limited and part of what we're testing).
/// </summary>
public static class JwtTestTokenFactory
{
    public static string Create(
        JwtOptions options,
        Guid userId,
        string role,
        Guid? organizationId = null,
        string? email = null,
        string? fullName = null,
        TimeSpan? lifetime = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("role", role),
        };

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));

        if (organizationId.HasValue && !string.Equals(role, "Guide", StringComparison.OrdinalIgnoreCase))
            claims.Add(new Claim("orgId", organizationId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(fullName))
            claims.Add(new Claim("name", fullName));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? options.Lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
