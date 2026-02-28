using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Auth;

internal static class AuthHandlers
{
    internal static IResult GetMe(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            return Results.Unauthorized();
        var role = user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role) ?? "";
        var fullName = user.FindFirstValue("name");
        return Results.Ok(new AuthMeResponse(role, userId, string.IsNullOrWhiteSpace(fullName) ? null : fullName));
    }

    internal static async Task<IResult> Login(
        LoginRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        IPasswordHasher<UserEntity> hasher,
        JwtOptions options,
        InforaCookieOptions cookieOptions,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var email = request.Email?.Trim().ToLowerInvariant();
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Results.BadRequest(new { message = "Email and password are required." });
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role)
        };

        if (user.OrganizationId.HasValue && !string.Equals(user.Role, "Guide", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("orgId", user.OrganizationId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            claims.Add(new Claim("name", user.FullName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(options.Lifetime),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var cookieOpts = cookieOptions.BuildCookieOptions(maxAge: options.Lifetime);
        httpContext.Response.Cookies.Append(InforaCookieOptions.AuthCookieName, accessToken, cookieOpts);
        return Results.Ok(new LoginResponse(accessToken, user.Role, user.Id, user.FullName));
    }

    internal static IResult Logout(HttpContext httpContext, InforaCookieOptions cookieOptions)
    {
        httpContext.Response.Cookies.Delete(InforaCookieOptions.AuthCookieName, cookieOptions.BuildClearCookieOptions());
        return Results.Ok();
    }
}
