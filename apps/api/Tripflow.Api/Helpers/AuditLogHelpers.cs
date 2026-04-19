using System.Security.Claims;
using System.Text.Json;

namespace Tripflow.Api.Helpers;

internal static class AuditLogHelpers
{
    internal const string CorrelationHeaderName = "X-Correlation-Id";

    internal static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    internal static string? MaskTcNo(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        return digits.Length <= 4
            ? new string('*', digits.Length)
            : $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }

    internal static bool TryResolveUserId(ClaimsPrincipal user, out Guid userId)
        => Guid.TryParse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    internal static string ResolveRole(ClaimsPrincipal user)
        => user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role) ?? "Anonymous";

    internal static bool TryResolveOrganizationId(HttpContext httpContext, out Guid organizationId)
    {
        var claimValue = httpContext.User.FindFirstValue("orgId");
        if (Guid.TryParse(claimValue, out organizationId))
        {
            return true;
        }

        var headerValue = httpContext.Request.Headers["X-Org-Id"].FirstOrDefault();
        return Guid.TryParse(headerValue, out organizationId);
    }

    internal static string EnsureCorrelationId(HttpContext httpContext)
    {
        var incoming = httpContext.Request.Headers[CorrelationHeaderName].FirstOrDefault();
        var correlationId = IsValidCorrelationId(incoming)
            ? incoming!.Trim()
            : Guid.NewGuid().ToString("n");

        httpContext.TraceIdentifier = correlationId;
        httpContext.Response.Headers[CorrelationHeaderName] = correlationId;
        return correlationId;
    }

    internal static bool ShouldSkipRequestLogging(PathString path)
        => path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/version", StringComparison.OrdinalIgnoreCase);

    internal static Dictionary<string, object?> CreateExtra(params (string Key, object? Value)[] pairs)
    {
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
            {
                extra[key] = value;
            }
        }

        return extra;
    }

    internal static Dictionary<string, object?> BuildMutationExtra(
        IEnumerable<string> changedFields,
        IDictionary<string, object?>? changes = null,
        IDictionary<string, object?>? extra = null)
    {
        var payload = extra is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(extra, StringComparer.Ordinal);

        var changed = changedFields.Distinct(StringComparer.Ordinal).ToArray();
        payload["changedFields"] = changed;
        if (changes is not null && changes.Count > 0)
        {
            payload["changes"] = changes;
        }

        return payload;
    }

    internal static void AddScalarChange<T>(
        ICollection<string> changedFields,
        IDictionary<string, object?> changes,
        string field,
        T before,
        T after)
    {
        if (EqualityComparer<T>.Default.Equals(before, after))
        {
            return;
        }

        changedFields.Add(field);
        changes[field] = CreateExtra(("before", before), ("after", after));
    }

    internal static void AddChangedField(ICollection<string> changedFields, string field)
    {
        if (!changedFields.Contains(field))
        {
            changedFields.Add(field);
        }
    }

    internal static string ToJsonElementString(object value)
        => JsonSerializer.SerializeToElement(value).GetRawText();

    private static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is 0 or > 64)
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
