using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Users;

internal static class UsersHandlers
{
    private const string RoleAdmin = "AgencyAdmin";
    private const string RoleGuide = "Guide";
    private const string RoleSuperAdmin = "SuperAdmin";

    private const string GuideActionCreated = "created";
    private const string GuideActionAttached = "attached";
    private const string GuideActionAlreadyAttached = "already_attached";

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

        if (string.Equals(roleClaim, RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(orgId) && Guid.TryParse(orgId, out resolvedOrgId))
            {
                // ok
            }
            else if (OrganizationHelpers.TryResolveOrganizationId(httpContext, out resolvedOrgId, out _))
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

        var roleFilter = role?.Trim();
        var normalizedRole = NormalizeRole(roleFilter);
        if (!string.IsNullOrWhiteSpace(roleFilter) && normalizedRole is null)
        {
            return Results.BadRequest(new { code = "invalid_role", message = "Role filter is invalid." });
        }

        var users = await BuildUsersQuery(db, resolvedOrgId, normalizedRole)
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
        var fullName = NormalizeFullName(request.FullName);
        var password = NormalizePassword(request.Password);

        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { message = "Email is required." });
        }

        var normalizedRole = NormalizeRole(request.Role);
        if (normalizedRole is null || string.Equals(normalizedRole, RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(normalizedRole, RoleGuide, StringComparison.OrdinalIgnoreCase))
        {
            return await EnsureGuideForOrganization(
                email,
                fullName,
                password,
                request.OrganizationId,
                db,
                hasher,
                ct);
        }

        if (!TryValidatePassword(password, out var passwordError))
        {
            return passwordError!;
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
        user.PasswordHash = hasher.HashPassword(user, password!);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/users/{user.Id}",
            new UserUpsertResponseDto(ToListItem(user), GuideActionCreated));
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
        var fullName = NormalizeFullName(request.FullName);
        var password = NormalizePassword(request.Password);

        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { message = "Email is required." });
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out _))
        {
            return Results.BadRequest(new { code = "org_required", message = "OrganizationId required." });
        }

        return await EnsureGuideForOrganization(email, fullName, password, orgId, db, hasher, ct);
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

        var password = NormalizePassword(request.NewPassword);
        if (!TryValidatePassword(password, out var passwordError))
        {
            return passwordError!;
        }

        var targetUser = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (targetUser is null)
        {
            return Results.NotFound(new { code = "user_not_found", message = "User not found." });
        }

        var roleClaim = user.FindFirstValue("role") ?? string.Empty;
        if (string.Equals(roleClaim, RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            // SuperAdmin can change any password.
        }
        else if (string.Equals(roleClaim, RoleAdmin, StringComparison.OrdinalIgnoreCase))
        {
            if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out _))
            {
                return Results.BadRequest(new { code = "org_required", message = "OrganizationId required." });
            }

            if (string.Equals(targetUser.Role, RoleGuide, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(
                    new { code = "guide_password_managed_globally", message = "Guide passwords are managed globally." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (targetUser.OrganizationId != orgId || string.Equals(targetUser.Role, RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { code = "forbidden", message = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
            }
        }
        else
        {
            return Results.Json(new { code = "forbidden", message = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        }

        targetUser.PasswordHash = hasher.HashPassword(targetUser, password!);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static IQueryable<UserEntity> BuildUsersQuery(TripflowDbContext db, Guid organizationId, string? normalizedRole)
    {
        var query = db.Users.AsNoTracking();

        if (string.Equals(normalizedRole, RoleGuide, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x =>
                x.Role == RoleGuide &&
                x.OrganizationGuideMemberships.Any(m => m.OrganizationId == organizationId));
        }

        if (!string.IsNullOrWhiteSpace(normalizedRole))
        {
            return query.Where(x => x.Role == normalizedRole && x.OrganizationId == organizationId);
        }

        return query.Where(x =>
            (x.Role == RoleGuide && x.OrganizationGuideMemberships.Any(m => m.OrganizationId == organizationId))
            || (x.Role != RoleGuide && x.OrganizationId == organizationId));
    }

    private static async Task<IResult> EnsureGuideForOrganization(
        string email,
        string? fullName,
        string? password,
        Guid organizationId,
        TripflowDbContext db,
        IPasswordHasher<UserEntity> hasher,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(x => x.OrganizationGuideMemberships)
            .SingleOrDefaultAsync(x => x.Email == email, ct);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return Results.BadRequest(new
                {
                    code = "password_required_for_new_guide",
                    message = "Password is required when creating a new guide."
                });
            }

            if (!TryValidatePassword(password, out var passwordError))
            {
                return passwordError!;
            }

            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = fullName,
                PasswordHash = string.Empty,
                Role = RoleGuide,
                OrganizationId = null,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            user.OrganizationGuideMemberships.Add(new OrganizationGuideEntity
            {
                OrganizationId = organizationId,
                GuideUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });

            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                return await EnsureGuideForOrganization(email, fullName, null, organizationId, db, hasher, ct);
            }

            return Results.Created(
                $"/api/users/{user.Id}",
                new UserUpsertResponseDto(ToListItem(user), GuideActionCreated));
        }

        if (!string.Equals(user.Role, RoleGuide, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                code = "email_belongs_to_non_guide",
                message = "Email already exists for a non-guide account."
            });
        }

        var updated = false;
        if (!string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = fullName;
            updated = true;
        }

        if (user.OrganizationId.HasValue)
        {
            user.OrganizationId = null;
            updated = true;
        }

        var membershipExists = user.OrganizationGuideMemberships.Any(x => x.OrganizationId == organizationId);
        if (!membershipExists)
        {
            user.OrganizationGuideMemberships.Add(new OrganizationGuideEntity
            {
                OrganizationId = organizationId,
                GuideUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });
            updated = true;
        }

        if (updated)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                var attachedUser = await db.Users.AsNoTracking()
                    .SingleAsync(x => x.Email == email, ct);
                return Results.Ok(new UserUpsertResponseDto(ToListItem(attachedUser), GuideActionAlreadyAttached));
            }
        }

        var action = membershipExists ? GuideActionAlreadyAttached : GuideActionAttached;
        return Results.Ok(new UserUpsertResponseDto(ToListItem(user), action));
    }

    private static bool TryValidatePassword(string? password, out IResult? error)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            error = Results.BadRequest(new
            {
                code = "password_too_short",
                message = "Password must be at least 8 characters."
            });
            return false;
        }

        error = null;
        return true;
    }

    private static UserListItemDto ToListItem(UserEntity user)
        => new(user.Id, user.Email, user.FullName, user.Role);

    private static string NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? NormalizeFullName(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizePassword(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        if (string.Equals(normalized, RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return RoleSuperAdmin;
        }

        return null;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && string.Equals(postgres.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
