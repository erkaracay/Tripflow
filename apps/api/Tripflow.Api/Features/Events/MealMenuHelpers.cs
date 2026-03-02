using System.Globalization;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Features.Events;

internal static class MealMenuHelpers
{
    internal const string MealActivityType = "Meal";
    internal const string OtherLabel = "Other";

    internal static bool IsMealActivity(string? type)
        => string.Equals(type?.Trim(), MealActivityType, StringComparison.OrdinalIgnoreCase);

    internal static string? NormalizeTitle(string? value)
        => NormalizeRequiredText(value);

    internal static string? NormalizeLabel(string? value)
        => NormalizeRequiredText(value);

    internal static string? NormalizeOtherText(string? value)
        => NormalizeOptionalText(value, 200);

    internal static string? NormalizeNote(string? value)
        => NormalizeOptionalText(value);

    internal static string? NormalizeRequiredText(string? value, int maxLength = 200)
    {
        var normalized = NormalizeOptionalText(value, maxLength);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    internal static string? NormalizeOptionalText(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (maxLength.HasValue && trimmed.Length > maxLength.Value)
        {
            trimmed = trimmed[..maxLength.Value];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    internal static MealOptionDto ToOptionDto(ActivityMealOptionEntity option)
        => new(option.Id, option.Label, option.SortOrder, option.IsActive);

    internal static MealGroupDto ToGroupDto(ActivityMealGroupEntity group)
        => new(
            group.Id,
            group.ActivityId,
            group.Title,
            group.SortOrder,
            group.AllowOther,
            group.AllowNote,
            group.IsActive,
            group.Options
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label)
                .Select(ToOptionDto)
                .ToArray());

    internal static IResult BadRequest(string code, string message)
        => Results.BadRequest(new { code, message });

    internal static IResult Conflict(string code, string message)
        => Results.Conflict(new { code, message });

    internal static string FormatUtc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    internal static bool TryParseOptionFilter(
        string? optionIdRaw,
        bool onlyOther,
        out Guid? optionId,
        out bool filterOther,
        out IResult? error)
    {
        optionId = null;
        filterOther = false;
        error = null;

        var trimmed = optionIdRaw?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            filterOther = true;
            return true;
        }

        if (string.Equals(trimmed, "other", StringComparison.OrdinalIgnoreCase))
        {
            filterOther = true;
            return true;
        }

        if (!Guid.TryParse(trimmed, out var parsedOptionId))
        {
            error = BadRequest("invalid_option_for_group", "Option filter is invalid.");
            return false;
        }

        if (onlyOther)
        {
            error = BadRequest("invalid_filter_combination", "onlyOther cannot be used together with a concrete optionId.");
            return false;
        }

        optionId = parsedOptionId;
        return true;
    }
}
