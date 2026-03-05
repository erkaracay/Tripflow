namespace Tripflow.Api.Features.Dev;

public sealed record ScenarioPresetDefaultsDto(
    int DayCount,
    int AccommodationCount,
    int ParticipantCount,
    int EquipmentTypeCount,
    string ActivityDensity,
    string MealMode,
    string FlightLegMode,
    bool IncludeFlights,
    int EventCheckInCoveragePercent,
    int MealSelectionCoveragePercent,
    string ParticipantNamingMode);

public sealed record ScenarioPresetDto(
    string Id,
    string Label,
    ScenarioPresetDefaultsDto Defaults);

public sealed record DevToolsCapabilitiesResponse(
    bool GeneralSeed,
    bool ScenarioEventGenerator,
    ScenarioPresetDto[] Presets);

public sealed record CreateScenarioEventRequest(
    string? Name,
    string? StartDate,
    int? DayCount,
    string? TimeZoneId,
    string? Preset,
    string? ActivityDensity,
    string? MealMode,
    string? FlightLegMode,
    int? AccommodationCount,
    int? ParticipantCount,
    int? EquipmentTypeCount,
    bool? IncludeFlights,
    int? MealSelectionCoveragePercent,
    int? EventCheckInCoveragePercent,
    string? ParticipantNamingMode,
    string? ParticipantNamePrefix,
    int? RandomSeed);

public sealed record ScenarioEventCountsDto(
    int Days,
    int Accommodations,
    int Activities,
    int MealActivities,
    int Participants,
    int EquipmentTypes,
    int MealGroups,
    int MealOptions,
    int MealSelections,
    int FlightSegments,
    int EventCheckIns);

public sealed record CreateScenarioEventResponse(
    Guid EventId,
    string Name,
    string StartDate,
    string EndDate,
    string TimeZoneId,
    string EventAccessCode,
    ScenarioEventCountsDto Created);

public sealed record DeleteScenarioEventResponse(Guid EventId, bool Deleted);
