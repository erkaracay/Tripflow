using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Users;

internal static class UsersHandlers
{
    private const string RoleAdmin = "AgencyAdmin";
    private const string RoleGuide = "Guide";

    internal static async Task<IResult> GetUsers(
        string? role,
        string? orgId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        TripflowDbContext db,
        CancellationToken ct)
    {
        var roleClaim = user.FindFirstValue("role") ?? string.Empty;
        Guid resolvedOrgId;

        if (string.Equals(roleClaim, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(orgId) && Guid.TryParse(orgId, out resolvedOrgId))
            {
                // ok
            }
            else if (OrganizationHelpers.TryResolveOrganizationId(httpContext, out resolvedOrgId, out var orgError))
            {
                // ok
            }
            else
            {
                return Results.BadRequest(new { code = "org_required", message = "OrganizationId required." });
            }
        }
        else
        {
            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out resolvedOrgId, out var error))
            {
                return error!;
            }
        }

        var query = db.Users.AsNoTracking()
            .Where(x => x.OrganizationId == resolvedOrgId);

        var roleFilter = role?.Trim();
        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            query = query.Where(x => x.Role.ToLower() == roleFilter.ToLower());
        }

        var users = await query
            .OrderBy(x => x.FullName ?? x.Email)
            .Select(x => new UserListItemDto(x.Id, x.Email, x.FullName, x.Role))
            .ToArrayAsync(ct);

        return Results.Ok(users);
    }

    internal static async Task<IResult> CreateUser(
        CreateUserRequest request,
        TripflowDbContext db,
        IPasswordHasher<UserEntity> hasher,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var email = NormalizeEmail(request.Email);
        var password = request.Password?.Trim() ?? string.Empty;
        var fullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return Results.BadRequest(new { code = "password_too_short", message = "Password must be at least 8 characters." });
        }

        var normalizedRole = NormalizeRole(request.Role);
        if (normalizedRole is null || normalizedRole == "SuperAdmin")
        {
            return Results.BadRequest(new { code = "invalid_role", message = "Role must be Admin or Guide." });
        }

        if (request.OrganizationId == Guid.Empty)
        {
            return Results.BadRequest(new { code = "org_required", message = "OrganizationId required." });
        }

        var orgExists = await db.Organizations.AsNoTracking()
            .AnyAsync(x => x.Id == request.OrganizationId, ct);
        if (!orgExists)
        {
            return Results.NotFound(new { code = "org_not_found", message = "Organization not found." });
        }

        var emailExists = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Email == email, ct);
        if (emailExists)
        {
            return Results.Conflict(new { code = "email_already_exists", message = "Email already exists." });
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            Role = normalizedRole,
            OrganizationId = request.OrganizationId,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/users/{user.Id}", new UserListItemDto(user.Id, user.Email, user.FullName, user.Role));
    }

    internal static async Task<IResult> CreateGuide(
        CreateGuideRequest request,
        HttpContext httpContext,
        TripflowDbContext db,
        IPasswordHasher<UserEntity> hasher,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var email = NormalizeEmail(request.Email);
        var password = request.Password?.Trim() ?? string.Empty;
        var fullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return Results.BadRequest(new { code = "password_too_short", message = "Password must be at least 8 characters." });
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out _))
        {
            return Results.BadRequest(new { code = "org_required", message = "OrganizationId required." });
        }

        var emailExists = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Email == email, ct);
        if (emailExists)
        {
            return Results.Conflict(new { code = "email_already_exists", message = "Email already exists." });
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            Role = RoleGuide,
            OrganizationId = orgId,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/users/{user.Id}", new UserListItemDto(user.Id, user.Email, user.FullName, user.Role));
    }

    internal static async Task<IResult> ChangePassword(
        Guid userId,
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        TripflowDbContext db,
        IPasswordHasher<UserEntity> hasher,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var password = request.NewPassword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return Results.BadRequest(new { code = "password_too_short", message = "Password must be at least 8 characters." });
        }

        var targetUser = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (targetUser is null)
        {
            return Results.NotFound(new { code = "user_not_found", message = "User not found." });
        }

        var roleClaim = user.FindFirstValue("role") ?? string.Empty;
        if (string.Equals(roleClaim, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            // SuperAdmin can change any user's password (including other SuperAdmins).
        }
        else if (string.Equals(roleClaim, RoleAdmin, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(targetUser.Role, RoleGuide, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { code = "invalid_role_target", message = "Only guide passwords can be changed." });
            }

            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out _))
            {
                return Results.BadRequest(new { code = "org_required", message = "OrganizationId required." });
            }

            if (targetUser.OrganizationId != orgId)
            {
                return Results.Json(new { code = "forbidden", message = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
            }
        }
        else
        {
            return Results.Json(new { code = "forbidden", message = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        }

        targetUser.PasswordHash = hasher.HashPassword(targetUser, password);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static string NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? NormalizeRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, RoleAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return RoleAdmin;
        }

        if (string.Equals(normalized, RoleGuide, StringComparison.OrdinalIgnoreCase))
        {
            return RoleGuide;
        }

        if (string.Equals(normalized, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return "SuperAdmin";
        }

        return null;
    }
}
