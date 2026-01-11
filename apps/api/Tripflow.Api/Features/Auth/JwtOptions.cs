using Microsoft.Extensions.Configuration;

namespace Tripflow.Api.Features.Auth;

public sealed record JwtOptions(string Issuer, string Audience, string Secret, TimeSpan Lifetime)
{
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["JWT_ISSUER"];
        var audience = configuration["JWT_AUDIENCE"];
        var secret = configuration["JWT_SECRET"];

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT config missing. Set JWT_ISSUER, JWT_AUDIENCE, and JWT_SECRET in user-secrets or environment variables.");
        }

        return new JwtOptions(issuer, audience, secret, TimeSpan.FromHours(8));
    }
}
