using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Dev;
using Tripflow.Api.Features.Events;

namespace Tripflow.Api.Data.Dev;

internal static class ScenarioEventGenerator
{
    private sealed record ScenarioPresetDefinition(
        string Id,
        string Label,
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
        string ParticipantNamingMode)
    {
        internal ScenarioPresetDto ToDto()
            => new(
                Id,
                Label,
                new ScenarioPresetDefaultsDto(
                    DayCount,
                    AccommodationCount,
                    ParticipantCount,
                    EquipmentTypeCount,
                    ActivityDensity,
                    MealMode,
                    FlightLegMode,
                    IncludeFlights,
                    EventCheckInCoveragePercent,
                    MealSelectionCoveragePercent,
                    ParticipantNamingMode));
    }

    internal sealed record ScenarioValidationError(string Code, string Field, string Message);

    internal sealed record ResolvedScenarioEventRequest(
        string? Name,
        DateOnly StartDate,
        int DayCount,
        int AccommodationCount,
        string TimeZoneId,
        string PresetId,
        string PresetLabel,
        string ActivityDensity,
        string MealMode,
        string FlightLegMode,
        int ParticipantCount,
        int EquipmentTypeCount,
        bool IncludeFlights,
        int MealSelectionCoveragePercent,
        int EventCheckInCoveragePercent,
        string ParticipantNamingMode,
        string? ParticipantNamePrefix,
        int? RandomSeed);

    private sealed record GeneratedParticipant(ParticipantEntity Participant, ParticipantDetailsEntity Details);
    private sealed record AccommodationPlanBoundary(DateOnly StartDate, DateOnly EndDate);
    private sealed record GeneratedAccommodationPlan(
        EventDocTabEntity DefaultHotelTab,
        DateOnly StartDate,
        DateOnly EndDate,
        int SortOrder);
    private sealed record GeneratedAccommodationLayout(
        List<EventDocTabEntity> Tabs,
        List<GeneratedAccommodationPlan> Plans,
        EventDocTabEntity OverrideHotelTab);

    private sealed record GeneratedMealConfig(
        int GroupCount,
        int OptionCount,
        List<ActivityMealGroupEntity> Groups);

    private sealed record ActivityTemplate(
        string Title,
        string Type,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        string? LocationName,
        string? Address,
        string? Notes,
        bool RequiresCheckIn = false,
        bool CheckInEnabled = false,
        string CheckInMode = "EntryOnly",
        string? MenuText = null,
        string? ProgramContent = null,
        string? SurveyUrl = null);

    private static readonly ScenarioPresetDefinition[] Presets =
    [
        new("minimal", "Minimal", 2, 2, 20, 1, "light", "none", "mixed", false, 0, 0, "random"),
        new("balanced", "Balanced", 3, 2, 40, 2, "normal", "breakfast_only", "mixed", true, 20, 70, "random"),
        new("operations_heavy", "Operations Heavy", 4, 3, 80, 4, "dense", "breakfast_and_dinner", "mixed", true, 35, 85, "random"),
        new("meal_heavy", "Meal Heavy", 3, 2, 36, 2, "normal", "breakfast_and_dinner", "mixed", false, 5, 95, "random"),
        new("flight_heavy", "Flight Heavy", 3, 2, 48, 2, "normal", "breakfast_only", "layover_heavy", true, 10, 30, "random"),
        new("checkin_heavy", "Check-in Heavy", 2, 2, 60, 1, "light", "none", "mixed", false, 90, 0, "random")
    ];

    private static readonly string[] SampleNames =
    [
        "Ayşe Demir",
        "Mehmet Kaya",
        "Elif Yılmaz",
        "Can Arslan",
        "Zeynep Acar",
        "Mert Yıldız",
        "Ece Şahin",
        "Kerem Polat",
        "Deniz Turan",
        "Seda Koç",
        "Emre Çakır",
        "Melis Aydın",
        "Ozan Aslan",
        "Nazlı Güneş",
        "Barış Eren",
        "İpek Tan",
        "Bora Demir",
        "Selin Öz",
        "Burak Yılmaz",
        "Derya Akın",
        "Tuna Arda",
        "Sevgi Koçak",
        "Ceren Ateş",
        "Kaan Gül",
        "Gökçe Sarı",
        "Tolga Bozkurt",
        "Eda Yaşar",
        "Umut Erdem",
        "Pelin Aksoy",
        "Hakan Köse",
        "Ege Karaman",
        "Aslı Kaplan",
        "Yasemin Er",
        "Cemal Ak",
        "Hande Ar",
        "İlker Tan"
    ];

    private static readonly string[] Cities = ["Istanbul", "Ankara", "Izmir", "Antalya", "Bursa", "Adana"];
    private static readonly string[] AgencyNames = ["Tripflow Travel", "Atlas Mice", "Mavi Rota", "Panorama Tours"];
    private static readonly string[] BoardTypes = ["BB", "HB", "AI", "RO"];
    private static readonly string[] Airports = ["IST", "SAW", "ESB", "ADB", "AYT", "ASR"];
    private static readonly string[] Airlines = ["THY", "Pegasus", "AJet", "SunExpress"];
    private static readonly string[] BreakfastOptions = ["Classic breakfast", "Vegetarian plate", "Light breakfast"];
    private static readonly string[] DinnerOptions = ["Meat menu", "Vegetarian menu", "Fish menu"];

    internal static ScenarioPresetDto[] GetPresetDtos()
        => Presets.Select(x => x.ToDto()).ToArray();

    internal static bool TryResolveRequest(
        CreateScenarioEventRequest request,
        out ResolvedScenarioEventRequest? resolved,
        out ScenarioValidationError? error)
    {
        resolved = null;
        error = null;

        var presetId = NormalizeLower(request.Preset);
        if (string.IsNullOrWhiteSpace(presetId))
        {
            presetId = "balanced";
        }

        var preset = Presets.FirstOrDefault(x => string.Equals(x.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
        {
            error = new ScenarioValidationError("invalid_scenario_request", "preset", "Preset is invalid.");
            return false;
        }

        if (!EventsHelpers.TryParseDate(request.StartDate, out var startDate))
        {
            error = new ScenarioValidationError("invalid_scenario_request", "startDate", "startDate is required and must use yyyy-MM-dd.");
            return false;
        }

        var dayCount = request.DayCount ?? preset.DayCount;
        if (dayCount is < 1 or > 14)
        {
            error = new ScenarioValidationError("invalid_day_count", "dayCount", "dayCount must be between 1 and 14.");
            return false;
        }

        var accommodationCount = request.AccommodationCount ?? preset.AccommodationCount;
        if (accommodationCount is < 1 or > 6)
        {
            error = new ScenarioValidationError("invalid_accommodation_count", "accommodationCount", "accommodationCount must be between 1 and 6.");
            return false;
        }
        accommodationCount = NormalizeAccommodationPlanCount(dayCount, accommodationCount);

        var participantCount = request.ParticipantCount ?? preset.ParticipantCount;
        if (participantCount is < 0 or > 500)
        {
            error = new ScenarioValidationError("invalid_participant_count", "participantCount", "participantCount must be between 0 and 500.");
            return false;
        }

        var equipmentTypeCount = request.EquipmentTypeCount ?? preset.EquipmentTypeCount;
        if (equipmentTypeCount is < 0 or > 10)
        {
            error = new ScenarioValidationError("invalid_equipment_type_count", "equipmentTypeCount", "equipmentTypeCount must be between 0 and 10.");
            return false;
        }

        var mealSelectionCoveragePercent = request.MealSelectionCoveragePercent ?? preset.MealSelectionCoveragePercent;
        if (mealSelectionCoveragePercent is < 0 or > 100)
        {
            error = new ScenarioValidationError("invalid_coverage_percent", "mealSelectionCoveragePercent", "mealSelectionCoveragePercent must be between 0 and 100.");
            return false;
        }

        var eventCheckInCoveragePercent = request.EventCheckInCoveragePercent ?? preset.EventCheckInCoveragePercent;
        if (eventCheckInCoveragePercent is < 0 or > 100)
        {
            error = new ScenarioValidationError("invalid_coverage_percent", "eventCheckInCoveragePercent", "eventCheckInCoveragePercent must be between 0 and 100.");
            return false;
        }

        if (!EventsHelpers.TryNormalizeTimeZoneId(request.TimeZoneId, out var timeZoneId, out var timeZoneErrorCode))
        {
            error = new ScenarioValidationError(timeZoneErrorCode, "timeZoneId", "Time zone is required and must be a valid IANA identifier.");
            return false;
        }

        var activityDensity = NormalizeLower(request.ActivityDensity) ?? preset.ActivityDensity;
        if (activityDensity is not ("light" or "normal" or "dense"))
        {
            error = new ScenarioValidationError("invalid_scenario_request", "activityDensity", "activityDensity must be light, normal, or dense.");
            return false;
        }

        var mealMode = NormalizeLower(request.MealMode) ?? preset.MealMode;
        if (mealMode is not ("none" or "breakfast_only" or "breakfast_and_dinner"))
        {
            error = new ScenarioValidationError("invalid_scenario_request", "mealMode", "mealMode must be none, breakfast_only, or breakfast_and_dinner.");
            return false;
        }

        var flightLegMode = NormalizeLower(request.FlightLegMode) ?? preset.FlightLegMode;
        if (flightLegMode is not ("mixed" or "direct_only" or "layover_heavy"))
        {
            error = new ScenarioValidationError("invalid_scenario_request", "flightLegMode", "flightLegMode must be mixed, direct_only, or layover_heavy.");
            return false;
        }

        var participantNamingMode = NormalizeLower(request.ParticipantNamingMode) ?? preset.ParticipantNamingMode;
        if (participantNamingMode is not ("random" or "prefix"))
        {
            error = new ScenarioValidationError("invalid_scenario_request", "participantNamingMode", "participantNamingMode must be random or prefix.");
            return false;
        }

        var participantNamePrefix = NormalizeNullableText(request.ParticipantNamePrefix);
        if (participantNamingMode == "prefix" && string.IsNullOrWhiteSpace(participantNamePrefix))
        {
            error = new ScenarioValidationError("invalid_participant_name_prefix", "participantNamePrefix", "participantNamePrefix is required for prefix naming mode.");
            return false;
        }

        resolved = new ResolvedScenarioEventRequest(
            NormalizeNullableText(request.Name),
            startDate,
            dayCount,
            accommodationCount,
            timeZoneId,
            preset.Id,
            preset.Label,
            activityDensity,
            mealMode,
            flightLegMode,
            participantCount,
            equipmentTypeCount,
            request.IncludeFlights ?? preset.IncludeFlights,
            mealSelectionCoveragePercent,
            eventCheckInCoveragePercent,
            participantNamingMode,
            participantNamePrefix,
            request.RandomSeed);

        return true;
    }

    private static int NormalizeAccommodationPlanCount(int dayCount, int requestedCount)
    {
        var maxPlanCount = GetMaxAccommodationPlanCount(dayCount);
        return Math.Clamp(requestedCount, 1, maxPlanCount);
    }

    private static int GetMaxAccommodationPlanCount(int dayCount)
    {
        if (dayCount <= 1)
        {
            return 1;
        }

        return Math.Max(1, Math.Min(6, dayCount - 1));
    }

    internal static async Task<CreateScenarioEventResponse> GenerateAsync(
        TripflowDbContext db,
        Guid organizationId,
        ResolvedScenarioEventRequest request,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var random = request.RandomSeed.HasValue ? new Random(request.RandomSeed.Value) : new Random();
        var eventAccessCode = await EventsHelpers.GenerateEventAccessCodeAsync(db, ct);
        var endDate = request.StartDate.AddDays(request.DayCount - 1);
        var effectiveAccommodationPlanCount = request.AccommodationCount;
        var guideUserId = await ResolveGuideUserIdAsync(db, organizationId, ct);

        var entity = new EventEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = request.Name ?? $"[DEV] {request.PresetLabel} {now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
            StartDate = request.StartDate,
            EndDate = endDate,
            TimeZoneId = request.TimeZoneId,
            GuideName = "Tripflow Guide",
            GuidePhone = "+905300000001",
            LeaderName = "Operations Lead",
            LeaderPhone = "+905300000002",
            EmergencyPhone = "+905300000099",
            WhatsappGroupUrl = "https://chat.whatsapp.com/tripflow-demo",
            EventAccessCode = eventAccessCode,
            CreatedAt = now,
            IsDeleted = false
        };

        if (guideUserId != Guid.Empty)
        {
            entity.EventGuides.Add(new EventGuideEntity
            {
                EventId = entity.Id,
                GuideUserId = guideUserId
            });
        }

        entity.Days.AddRange(EventsHelpers.CreateDefaultDays(entity));
        var accommodationLayout = CreateScenarioAccommodationLayout(entity, now, effectiveAccommodationPlanCount);
        entity.DocTabs.AddRange(accommodationLayout.Tabs);
        entity.Items.AddRange(EventsHelpers.CreateDefaultEventItems(entity, request.EquipmentTypeCount));

        entity.Portal = new EventPortalEntity
        {
            EventId = entity.Id,
            OrganizationId = organizationId,
            PortalJson = JsonSerializer.Serialize(
                BuildPortalInfo(entity, entity.Days.OrderBy(x => x.SortOrder).ToList(), Array.Empty<EventActivityEntity>()),
                EventsHelpers.JsonOptions),
            UpdatedAt = now
        };

        db.Events.Add(entity);
        await db.SaveChangesAsync(ct);

        var accommodationSegments = CreateAccommodationSegments(entity, accommodationLayout.Plans, now);
        if (accommodationSegments.Count > 0)
        {
            db.EventAccommodationSegments.AddRange(accommodationSegments);
            await db.SaveChangesAsync(ct);
        }

        var days = entity.Days.OrderBy(x => x.SortOrder).ToList();
        var activities = BuildActivities(entity, days, request);
        if (activities.Count > 0)
        {
            db.EventActivities.AddRange(activities);
            await db.SaveChangesAsync(ct);
        }

        var mealConfig = await CreateMealConfigurationAsync(db, entity, activities, now, ct);
        var participants = await CreateParticipantsAsync(db, entity, request, random, now, ct);
        var participantEntities = participants.Select(x => x.Participant).ToList();

        var accommodationAssignments = CreateAccommodationAssignments(
            entity,
            accommodationSegments,
            accommodationLayout.OverrideHotelTab,
            participants,
            now);
        if (accommodationAssignments.Count > 0)
        {
            db.ParticipantAccommodationAssignments.AddRange(accommodationAssignments);
            await db.SaveChangesAsync(ct);
        }

        MirrorLegacyAccommodationDetails(participants, accommodationSegments, accommodationAssignments);
        await db.SaveChangesAsync(ct);

        var flightSegments = request.IncludeFlights
            ? CreateFlightSegments(entity, participants, random, request.FlightLegMode)
            : new List<ParticipantFlightSegmentEntity>();

        if (flightSegments.Count > 0)
        {
            db.ParticipantFlightSegments.AddRange(flightSegments);
            await db.SaveChangesAsync(ct);
        }

        var mealSelections = CreateMealSelections(entity, mealConfig.Groups, participantEntities, request, random, now);
        if (mealSelections.Count > 0)
        {
            db.ParticipantMealSelections.AddRange(mealSelections);
            await db.SaveChangesAsync(ct);
        }

        var eventCheckIns = CreateEventCheckIns(entity, participantEntities, request, random);
        if (eventCheckIns.Count > 0)
        {
            db.CheckIns.AddRange(eventCheckIns);
            await db.SaveChangesAsync(ct);
        }

        entity.Portal!.PortalJson = JsonSerializer.Serialize(BuildPortalInfo(entity, days, activities), EventsHelpers.JsonOptions);
        entity.Portal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new CreateScenarioEventResponse(
            entity.Id,
            entity.Name,
            entity.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            entity.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            entity.TimeZoneId ?? request.TimeZoneId,
            entity.EventAccessCode,
            new ScenarioEventCountsDto(
                days.Count,
                effectiveAccommodationPlanCount,
                activities.Count,
                activities.Count(x => IsMealActivity(x.Type)),
                participantEntities.Count,
                entity.Items.Count,
                mealConfig.GroupCount,
                mealConfig.OptionCount,
                mealSelections.Count,
                flightSegments.Count,
                eventCheckIns.Count));
    }

    private static async Task<Guid> ResolveGuideUserIdAsync(TripflowDbContext db, Guid organizationId, CancellationToken ct)
    {
        var guideUserId = await db.OrganizationGuides.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.GuideUserId)
            .FirstOrDefaultAsync(ct);

        if (guideUserId != Guid.Empty)
        {
            return guideUserId;
        }

        return await db.Users.AsNoTracking()
            .Where(x => x.Role == "Guide")
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static GeneratedAccommodationLayout CreateScenarioAccommodationLayout(
        EventEntity entity,
        DateTime createdAtUtc,
        int accommodationPlanCount)
    {
        var tabs = EventsHelpers.CreateDefaultDocTabs(entity, createdAtUtc);
        var insuranceTab = tabs.FirstOrDefault(x => string.Equals(x.Type, "Insurance", StringComparison.Ordinal));
        var transferTab = tabs.FirstOrDefault(x => string.Equals(x.Type, "Transfer", StringComparison.Ordinal));

        tabs.RemoveAll(x => string.Equals(x.Type, "Hotel", StringComparison.Ordinal));
        tabs.RemoveAll(x => string.Equals(x.Type, "Insurance", StringComparison.Ordinal));
        tabs.RemoveAll(x => string.Equals(x.Type, "Transfer", StringComparison.Ordinal));

        var planBoundaries = BuildAccommodationPlanBoundaries(entity.StartDate, entity.EndDate, accommodationPlanCount);
        var defaultPlans = new List<GeneratedAccommodationPlan>(planBoundaries.Count);
        for (var index = 0; index < planBoundaries.Count; index++)
        {
            var boundary = planBoundaries[index];
            var hotelTab = new EventDocTabEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = entity.OrganizationId,
                EventId = entity.Id,
                Type = "Hotel",
                IsActive = true,
                CreatedAt = createdAtUtc
            };

            hotelTab.Title = $"Konaklama {index + 1}";
            hotelTab.SortOrder = index + 1;
            hotelTab.ContentJson = BuildAccommodationContentJson(
                hotelName: $"{entity.Name} Konaklama {index + 1}",
                address: ResolveAccommodationAddress(index),
                phone: ResolveAccommodationPhone(index),
                checkInDate: boundary.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                checkOutDate: boundary.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                checkInNote: index == 0
                    ? "Giriş 14:00 itibarıyla"
                    : $"Konaklama {index + 1} için giriş 15:00 itibarıyla",
                checkOutNote: "Çıkış 12:00");

            tabs.Add(hotelTab);
            defaultPlans.Add(new GeneratedAccommodationPlan(hotelTab, boundary.StartDate, boundary.EndDate, index + 1));
        }

        var overrideBoundary = planBoundaries.Count > 0
            ? planBoundaries[^1]
            : new AccommodationPlanBoundary(entity.StartDate, entity.EndDate);
        var overrideHotelTab = new EventDocTabEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = entity.OrganizationId,
            EventId = entity.Id,
            Type = "Hotel",
            Title = "Konaklama Override",
            SortOrder = defaultPlans.Count + 1,
            IsActive = true,
            CreatedAt = createdAtUtc,
            ContentJson = BuildAccommodationContentJson(
                hotelName: $"{entity.Name} Konaklama Override",
                address: "Marina Cad. No:42",
                phone: "+90 252 555 0019",
                checkInDate: overrideBoundary.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                checkOutDate: overrideBoundary.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                checkInNote: "Override konaklama için giriş 15:00 itibarıyla",
                checkOutNote: "Çıkış 11:30")
        };
        tabs.Add(overrideHotelTab);

        var nextSortOrder = defaultPlans.Count + 2;

        if (insuranceTab is not null)
        {
            insuranceTab.SortOrder = nextSortOrder++;
            insuranceTab.ContentJson = JsonSerializer.Serialize(new
            {
                companyName = "Tripflow Sigorta",
                policyNo = $"POL-{entity.StartDate:yyyyMMdd}",
                startDate = entity.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate = entity.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
            tabs.Add(insuranceTab);
        }

        if (transferTab is not null)
        {
            transferTab.SortOrder = nextSortOrder;
            transferTab.ContentJson = JsonSerializer.Serialize(new
            {
                provider = "Tripflow Transport",
                arrivalMeetingPoint = "Airport exit gate",
                departureMeetingPoint = "Konaklama lobisi",
                note = "Driver contact details are shared on the event day."
            });
            tabs.Add(transferTab);
        }

        return new GeneratedAccommodationLayout(
            tabs.OrderBy(x => x.SortOrder).ToList(),
            defaultPlans,
            overrideHotelTab);
    }

    private static string BuildAccommodationContentJson(
        string hotelName,
        string address,
        string phone,
        string checkInDate,
        string checkOutDate,
        string checkInNote,
        string checkOutNote)
    {
        return JsonSerializer.Serialize(new
        {
            hotelName,
            address,
            phone,
            checkInDate,
            checkOutDate,
            checkInNote,
            checkOutNote
        });
    }

    private static List<AccommodationPlanBoundary> BuildAccommodationPlanBoundaries(
        DateOnly eventStartDate,
        DateOnly eventEndDate,
        int accommodationPlanCount)
    {
        var totalNights = Math.Max(0, eventEndDate.DayNumber - eventStartDate.DayNumber);
        if (totalNights == 0)
        {
            return [new AccommodationPlanBoundary(eventStartDate, eventStartDate)];
        }

        var safeCount = Math.Max(1, accommodationPlanCount);
        var boundaries = new List<AccommodationPlanBoundary>(safeCount);
        for (var index = 0; index < safeCount; index++)
        {
            var startOffset = (int)Math.Floor(index * totalNights / (double)safeCount);
            var endOffset = (int)Math.Floor((index + 1) * totalNights / (double)safeCount);
            if (endOffset <= startOffset)
            {
                endOffset = Math.Min(totalNights, startOffset + 1);
            }

            boundaries.Add(new AccommodationPlanBoundary(
                eventStartDate.AddDays(startOffset),
                eventStartDate.AddDays(endOffset)));
        }

        return boundaries;
    }

    private static string ResolveAccommodationAddress(int index)
        => index switch
        {
            0 => "Merkez Mah. 10. Sokak No:5",
            1 => "Sahil Cad. No:24",
            2 => "Kale Yolu No:18",
            _ => $"Rota Bulvarı No:{30 + index}"
        };

    private static string ResolveAccommodationPhone(int index)
        => $"+90 {(212 + (index % 4) * 10).ToString(CultureInfo.InvariantCulture)} 555 00{index + 1:00}";
    
    private static List<EventAccommodationSegmentEntity> CreateAccommodationSegments(
        EventEntity entity,
        IReadOnlyList<GeneratedAccommodationPlan> plans,
        DateTime nowUtc)
    {
        return plans
            .Select(plan => new EventAccommodationSegmentEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = entity.OrganizationId,
                EventId = entity.Id,
                DefaultAccommodationDocTabId = plan.DefaultHotelTab.Id,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                SortOrder = plan.SortOrder,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            })
            .ToList();
    }

    private static List<EventActivityEntity> BuildActivities(
        EventEntity entity,
        IReadOnlyList<EventDayEntity> days,
        ResolvedScenarioEventRequest request)
    {
        var activities = new List<EventActivityEntity>();

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var templates = BuildDayTemplates(index, days.Count, request);

            foreach (var template in templates)
            {
                activities.Add(new EventActivityEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = entity.OrganizationId,
                    EventId = entity.Id,
                    EventDayId = day.Id,
                    Title = template.Title,
                    Type = template.Type,
                    StartTime = template.StartTime,
                    EndTime = template.EndTime,
                    LocationName = template.LocationName,
                    Address = template.Address,
                    Notes = template.Notes,
                    ProgramContent = template.ProgramContent,
                    MenuText = template.MenuText,
                    SurveyUrl = template.SurveyUrl,
                    RequiresCheckIn = template.RequiresCheckIn,
                    CheckInEnabled = template.CheckInEnabled,
                    CheckInMode = template.CheckInMode
                });
            }
        }

        return activities;
    }

    private static List<ActivityTemplate> BuildDayTemplates(int dayIndex, int totalDays, ResolvedScenarioEventRequest request)
    {
        var dayNumber = dayIndex + 1;
        var isLastDay = dayIndex == totalDays - 1;
        var templates = new List<ActivityTemplate>();
        var includeDinner = request.MealMode == "breakfast_and_dinner";

        if (request.MealMode is "breakfast_only" or "breakfast_and_dinner")
        {
            templates.Add(new ActivityTemplate(
                $"Breakfast Service - Day {dayNumber}",
                "Meal",
                new TimeOnly(8, 30),
                new TimeOnly(9, 15),
                "Hotel restaurant",
                "Hotel restaurant",
                "Breakfast selection window",
                MenuText: string.Join(" / ", BreakfastOptions)));
        }

        if (request.ActivityDensity == "light")
        {
            templates.Add(new ActivityTemplate(
                isLastDay ? $"Closing Tour - Day {dayNumber}" : $"Signature Visit - Day {dayNumber}",
                "Other",
                new TimeOnly(10, 0),
                new TimeOnly(12, 0),
                "Main venue",
                $"Route stop {dayNumber}",
                "Core guided visit",
                RequiresCheckIn: true));

            templates.Add(new ActivityTemplate(
                isLastDay ? "Feedback & departure notes" : "Free time and local walk",
                "Other",
                new TimeOnly(17, 0),
                new TimeOnly(18, 0),
                "City center",
                "City center",
                isLastDay ? "Wrap-up and final notes" : "Independent time block",
                SurveyUrl: isLastDay ? "https://example.com/tripflow/feedback" : null,
                ProgramContent: isLastDay ? "Collect feedback before departure." : null));

            if (includeDinner)
            {
                templates.Add(BuildDinnerTemplate(dayNumber, new TimeOnly(19, 0), new TimeOnly(20, 0)));
            }
        }
        else if (request.ActivityDensity == "normal")
        {
            templates.Add(new ActivityTemplate(
                $"Morning Tour - Day {dayNumber}",
                "Other",
                new TimeOnly(10, 0),
                new TimeOnly(12, 0),
                "Historic center",
                "Historic center",
                "Primary guided route",
                RequiresCheckIn: true));

            templates.Add(new ActivityTemplate(
                $"Afternoon Visit - Day {dayNumber}",
                "Other",
                new TimeOnly(14, 0),
                new TimeOnly(16, 30),
                "Museum district",
                "Museum district",
                "Secondary venue",
                RequiresCheckIn: true));

            templates.Add(new ActivityTemplate(
                isLastDay ? "Feedback Session" : "Evening Wrap-up",
                "Other",
                new TimeOnly(18, 0),
                new TimeOnly(19, 0),
                "Meeting room",
                "Meeting room",
                isLastDay ? "Collect feedback and closing remarks" : "Daily briefing and reminders",
                SurveyUrl: isLastDay ? "https://example.com/tripflow/feedback" : null,
                ProgramContent: isLastDay ? "Share highlights and collect final survey responses." : null));

            if (includeDinner)
            {
                templates.Add(BuildDinnerTemplate(dayNumber, new TimeOnly(19, 15), new TimeOnly(20, 15)));
            }
        }
        else
        {
            templates.Add(new ActivityTemplate(
                $"Morning Briefing - Day {dayNumber}",
                "Other",
                new TimeOnly(9, 15),
                new TimeOnly(9, 45),
                "Hotel lobby",
                "Hotel lobby",
                "Bus boarding and readiness check"));

            templates.Add(new ActivityTemplate(
                $"Main Excursion - Day {dayNumber}",
                "Other",
                new TimeOnly(10, 0),
                new TimeOnly(12, 30),
                "Main venue",
                "Main venue",
                "High-traffic guided activity",
                RequiresCheckIn: true));

            templates.Add(new ActivityTemplate(
                $"Afternoon Route - Day {dayNumber}",
                "Other",
                new TimeOnly(14, 0),
                new TimeOnly(16, 30),
                "Secondary venue",
                "Secondary venue",
                "Operational follow-up stop",
                RequiresCheckIn: true));

            if (includeDinner)
            {
                templates.Add(BuildDinnerTemplate(dayNumber, new TimeOnly(19, 0), new TimeOnly(20, 0)));
            }

            templates.Add(new ActivityTemplate(
                isLastDay ? "Feedback and departure prep" : "Evening Operations Wrap-up",
                "Other",
                new TimeOnly(20, 15),
                new TimeOnly(21, 0),
                "Meeting room",
                "Meeting room",
                isLastDay ? "Final checklist and survey" : "Review next-day operations",
                SurveyUrl: isLastDay ? "https://example.com/tripflow/feedback" : null));
        }

        return templates;
    }

    private static ActivityTemplate BuildDinnerTemplate(int dayNumber, TimeOnly startTime, TimeOnly endTime)
        => new(
            $"Dinner Service - Day {dayNumber}",
            "Meal",
            startTime,
            endTime,
            "Dinner venue",
            "Dinner venue",
            "Dinner selection window",
            MenuText: string.Join(" / ", DinnerOptions));

    private static async Task<GeneratedMealConfig> CreateMealConfigurationAsync(
        TripflowDbContext db,
        EventEntity entity,
        IReadOnlyList<EventActivityEntity> activities,
        DateTime now,
        CancellationToken ct)
    {
        var mealActivities = activities.Where(x => IsMealActivity(x.Type)).ToList();
        if (mealActivities.Count == 0)
        {
            return new GeneratedMealConfig(0, 0, []);
        }

        var groups = new List<ActivityMealGroupEntity>(mealActivities.Count);
        var optionCount = 0;

        foreach (var activity in mealActivities)
        {
            var isDinner = activity.Title.Contains("Dinner", StringComparison.OrdinalIgnoreCase);
            var optionLabels = isDinner ? DinnerOptions : BreakfastOptions;
            var group = new ActivityMealGroupEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = entity.OrganizationId,
                EventId = entity.Id,
                ActivityId = activity.Id,
                Title = isDinner ? "Dinner choice" : "Breakfast choice",
                SortOrder = 1,
                AllowOther = true,
                AllowNote = true,
                IsActive = true
            };

            for (var optionIndex = 0; optionIndex < optionLabels.Length; optionIndex++)
            {
                group.Options.Add(new ActivityMealOptionEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = entity.OrganizationId,
                    GroupId = group.Id,
                    Label = optionLabels[optionIndex],
                    SortOrder = optionIndex + 1,
                    IsActive = true
                });
            }

            optionCount += group.Options.Count;
            groups.Add(group);
        }

        db.ActivityMealGroups.AddRange(groups);
        await db.SaveChangesAsync(ct);

        return new GeneratedMealConfig(groups.Count, optionCount, groups);
    }

    private static async Task<List<GeneratedParticipant>> CreateParticipantsAsync(
        TripflowDbContext db,
        EventEntity entity,
        ResolvedScenarioEventRequest request,
        Random random,
        DateTime now,
        CancellationToken ct)
    {
        var participants = new List<GeneratedParticipant>(request.ParticipantCount);
        var existingCodes = new HashSet<string>(
            await db.Participants.AsNoTracking().Select(x => x.CheckInCode).ToListAsync(ct),
            StringComparer.Ordinal);

        for (var index = 1; index <= request.ParticipantCount; index++)
        {
            var (firstName, lastName, fullName) = BuildParticipantName(request, random, index);
            var tcNo = GenerateTcNo(index, request.RandomSeed);
            var participant = new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                EventId = entity.Id,
                OrganizationId = entity.OrganizationId,
                FirstName = firstName,
                LastName = lastName,
                FullName = fullName,
                Phone = $"+9053{(20000000 + index):00000000}",
                Email = $"scenario-{entity.Id.ToString("N")[..6]}-{index:000}@demo.local",
                TcNo = tcNo,
                BirthDate = new DateOnly(1985, 1, 1).AddDays(index * 37),
                Gender = (index % 3) switch
                {
                    0 => ParticipantGender.Female,
                    1 => ParticipantGender.Male,
                    _ => ParticipantGender.Other
                },
                CheckInCode = GenerateCheckInCode(existingCodes),
                CreatedAt = now,
                WillNotAttend = false
            };

            var details = CreateParticipantDetails(entity, participant.Id, random, index);
            participants.Add(new GeneratedParticipant(participant, details));
        }

        db.Participants.AddRange(participants.Select(x => x.Participant));
        db.ParticipantDetails.AddRange(participants.Select(x => x.Details));
        await db.SaveChangesAsync(ct);

        return participants;
    }

    private static ParticipantDetailsEntity CreateParticipantDetails(
        EventEntity entity,
        Guid participantId,
        Random random,
        int index)
    {
        var arrivalOrigin = Airports[index % Airports.Length];
        var arrivalDestination = Airports[(index + 2) % Airports.Length];
        if (string.Equals(arrivalOrigin, arrivalDestination, StringComparison.OrdinalIgnoreCase))
        {
            arrivalDestination = "AYT";
        }

        var returnOrigin = arrivalDestination;
        var returnDestination = arrivalOrigin;
        var arrivalAirline = Airlines[index % Airlines.Length];
        var returnAirline = Airlines[(index + 1) % Airlines.Length];
        var arrivalDepartureTime = new TimeOnly(7 + (index % 4), index % 2 == 0 ? 10 : 40);
        var arrivalArrivalTime = AddMinutes(arrivalDepartureTime, 90);
        var returnDepartureTime = new TimeOnly(17 + (index % 3), index % 2 == 0 ? 0 : 30);
        var returnArrivalTime = AddMinutes(returnDepartureTime, 95);
        var arrivalPieces = (index % 2) + 1;
        var returnPieces = ((index + 1) % 2) + 1;

        return new ParticipantDetailsEntity
        {
            ParticipantId = participantId,
            AgencyName = AgencyNames[index % AgencyNames.Length],
            City = Cities[index % Cities.Length],
            FlightCity = Cities[(index + 1) % Cities.Length],
            TicketNo = $"TKT-{index:0000}",
            ArrivalTicketNo = $"TKT-{index:0000}-OUT",
            ReturnTicketNo = $"TKT-{index:0000}-RET",
            AttendanceStatus = index % 8 == 0 ? "Late" : "Confirmed",
            InsuranceCompanyName = "Tripflow Sigorta",
            InsurancePolicyNo = $"POL-{index:0000}",
            InsuranceStartDate = entity.StartDate,
            InsuranceEndDate = entity.EndDate,
            ArrivalAirline = arrivalAirline,
            ArrivalDepartureAirport = arrivalOrigin,
            ArrivalArrivalAirport = arrivalDestination,
            ArrivalFlightCode = $"TF{100 + index}",
            ArrivalFlightDate = entity.StartDate.AddDays(-1),
            ArrivalDepartureTime = arrivalDepartureTime,
            ArrivalArrivalTime = arrivalArrivalTime,
            ArrivalPnr = $"ARR{index:0000}",
            ArrivalBaggagePieces = arrivalPieces,
            ArrivalBaggageTotalKg = arrivalPieces == 1 ? 23 : 30,
            ArrivalBaggageAllowance = $"{arrivalPieces} pc { (arrivalPieces == 1 ? 23 : 30)} kg",
            ReturnAirline = returnAirline,
            ReturnDepartureAirport = returnOrigin,
            ReturnArrivalAirport = returnDestination,
            ReturnFlightCode = $"TF{300 + index}",
            ReturnFlightDate = entity.EndDate,
            ReturnDepartureTime = returnDepartureTime,
            ReturnArrivalTime = returnArrivalTime,
            ReturnPnr = $"RET{index:0000}",
            ReturnBaggagePieces = returnPieces,
            ReturnBaggageTotalKg = returnPieces == 1 ? 23 : 30,
            ReturnBaggageAllowance = $"{returnPieces} pc { (returnPieces == 1 ? 23 : 30)} kg",
            ArrivalTransferPickupTime = new TimeOnly(6 + (index % 3), 45),
            ArrivalTransferPickupPlace = "Airport arrival gate",
            ArrivalTransferDropoffPlace = "Hotel lobby",
            ArrivalTransferVehicle = index % 2 == 0 ? "Mercedes Sprinter" : "Mercedes Vito",
            ArrivalTransferPlate = $"34 TF {200 + index:000}",
            ArrivalTransferDriverInfo = $"Driver {index % 5 + 1} · +90 530 111 22 {index % 10}{index % 10}",
            ArrivalTransferNote = "Tripflow signboard at the arrivals hall.",
            ReturnTransferPickupTime = new TimeOnly(15 + (index % 3), 50),
            ReturnTransferPickupPlace = "Hotel lobby",
            ReturnTransferDropoffPlace = "Airport departure gate",
            ReturnTransferVehicle = index % 2 == 0 ? "Mercedes Vito" : "Mercedes Sprinter",
            ReturnTransferPlate = $"34 TF {500 + index:000}",
            ReturnTransferDriverInfo = $"Driver {index % 4 + 1} · +90 530 333 44 {index % 10}{index % 10}",
            ReturnTransferNote = random.Next(0, 4) == 0 ? "Allow extra time for security." : "Be in the lobby 15 minutes early."
        };
    }

    private static List<ParticipantAccommodationAssignmentEntity> CreateAccommodationAssignments(
        EventEntity entity,
        IReadOnlyList<EventAccommodationSegmentEntity> segments,
        EventDocTabEntity overrideHotelTab,
        IReadOnlyList<GeneratedParticipant> participants,
        DateTime nowUtc)
    {
        if (segments.Count == 0 || participants.Count == 0)
        {
            return [];
        }

        var assignments = new List<ParticipantAccommodationAssignmentEntity>(segments.Count * participants.Count);
        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            var overrideParticipantIds = segmentIndex == segments.Count - 1
                ? SelectOverrideParticipantIds(participants, segmentIndex)
                : [];

            var defaultParticipants = participants
                .Where(x => !overrideParticipantIds.Contains(x.Participant.Id))
                .ToList();
            var overrideParticipants = participants
                .Where(x => overrideParticipantIds.Contains(x.Participant.Id))
                .ToList();

            assignments.AddRange(CreateAccommodationAssignmentsForGroup(
                entity,
                segment,
                defaultParticipants,
                overrideAccommodationDocTabId: null,
                segmentIndex,
                accommodationGroupOffset: 0,
                nowUtc));

            assignments.AddRange(CreateAccommodationAssignmentsForGroup(
                entity,
                segment,
                overrideParticipants,
                overrideHotelTab.Id,
                segmentIndex,
                accommodationGroupOffset: 1,
                nowUtc));
        }

        return assignments;
    }

    private static List<ParticipantAccommodationAssignmentEntity> CreateAccommodationAssignmentsForGroup(
        EventEntity entity,
        EventAccommodationSegmentEntity segment,
        IReadOnlyList<GeneratedParticipant> participants,
        Guid? overrideAccommodationDocTabId,
        int segmentIndex,
        int accommodationGroupOffset,
        DateTime nowUtc)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        var assignments = new List<ParticipantAccommodationAssignmentEntity>(participants.Count);
        var groupSizes = BuildRoomGroupSizes(participants.Count);
        var participantOffset = 0;
        var roomBase = ((segmentIndex + 1) * 100) + (accommodationGroupOffset * 50);

        for (var groupIndex = 0; groupIndex < groupSizes.Count; groupIndex++)
        {
            var groupSize = groupSizes[groupIndex];
            var roomNo = (roomBase + groupIndex + 1).ToString(CultureInfo.InvariantCulture);
            var roomType = ResolveScenarioRoomType(groupSize, groupIndex + accommodationGroupOffset);
            var boardType = BoardTypes[(segmentIndex + groupIndex + accommodationGroupOffset) % BoardTypes.Length];
            var groupParticipants = participants.Skip(participantOffset).Take(groupSize).ToArray();

            foreach (var generatedParticipant in groupParticipants)
            {
                assignments.Add(new ParticipantAccommodationAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = entity.OrganizationId,
                    EventId = entity.Id,
                    ParticipantId = generatedParticipant.Participant.Id,
                    SegmentId = segment.Id,
                    OverrideAccommodationDocTabId = overrideAccommodationDocTabId,
                    RoomNo = roomNo,
                    RoomType = roomType,
                    BoardType = boardType,
                    PersonNo = groupSize.ToString(CultureInfo.InvariantCulture),
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }

            participantOffset += groupSize;
        }

        return assignments;
    }

    private static HashSet<Guid> SelectOverrideParticipantIds(
        IReadOnlyList<GeneratedParticipant> participants,
        int segmentIndex)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        var targetCount = participants.Count >= 6
            ? Math.Min(5, Math.Max(2, participants.Count / 8))
            : 1;

        var selected = participants
            .Where((_, index) => ((index + segmentIndex) % 7) == 0)
            .Take(targetCount)
            .Select(x => x.Participant.Id)
            .ToHashSet();

        if (selected.Count >= targetCount)
        {
            return selected;
        }

        foreach (var participantId in participants.Select(x => x.Participant.Id))
        {
            selected.Add(participantId);
            if (selected.Count >= targetCount)
            {
                break;
            }
        }

        return selected;
    }

    private static List<int> BuildRoomGroupSizes(int participantCount)
    {
        var sizes = new List<int>();
        var remaining = participantCount;
        var groupIndex = 0;

        while (remaining > 0)
        {
            var desired = groupIndex % 6 == 4
                ? 1
                : groupIndex % 5 == 2
                    ? 3
                    : 2;

            desired = Math.Min(desired, remaining);
            if (remaining - desired == 1 && remaining >= 3)
            {
                desired = desired == 3 ? 2 : 3;
            }

            sizes.Add(desired);
            remaining -= desired;
            groupIndex++;
        }

        return sizes;
    }

    private static string ResolveScenarioRoomType(int groupSize, int roomIndex)
        => groupSize switch
        {
            <= 1 => "Single",
            2 => roomIndex % 2 == 0 ? "Twin" : "Double",
            _ => "Triple"
        };

    private static void MirrorLegacyAccommodationDetails(
        IReadOnlyList<GeneratedParticipant> participants,
        IReadOnlyList<EventAccommodationSegmentEntity> segments,
        IReadOnlyList<ParticipantAccommodationAssignmentEntity> assignments)
    {
        var firstSegment = segments.OrderBy(x => x.SortOrder).FirstOrDefault();
        if (firstSegment is null)
        {
            return;
        }

        var firstSegmentAssignments = assignments
            .Where(x => x.SegmentId == firstSegment.Id)
            .ToDictionary(x => x.ParticipantId);

        foreach (var generatedParticipant in participants)
        {
            if (!firstSegmentAssignments.TryGetValue(generatedParticipant.Participant.Id, out var assignment))
            {
                continue;
            }

            generatedParticipant.Details.AccommodationDocTabId =
                assignment.OverrideAccommodationDocTabId ?? firstSegment.DefaultAccommodationDocTabId;
            generatedParticipant.Details.RoomNo = assignment.RoomNo;
            generatedParticipant.Details.RoomType = assignment.RoomType;
            generatedParticipant.Details.BoardType = assignment.BoardType;
            generatedParticipant.Details.PersonNo = assignment.PersonNo;
            generatedParticipant.Details.HotelCheckInDate = firstSegment.StartDate;
            generatedParticipant.Details.HotelCheckOutDate = firstSegment.EndDate;
        }
    }

    private static List<ParticipantFlightSegmentEntity> CreateFlightSegments(
        EventEntity entity,
        IReadOnlyList<GeneratedParticipant> participants,
        Random random,
        string flightLegMode)
    {
        var segments = new List<ParticipantFlightSegmentEntity>(participants.Count * 3);

        for (var index = 0; index < participants.Count; index++)
        {
            var generated = CreateFlightSegmentsForParticipant(entity, participants[index], index + 1, random, flightLegMode);
            segments.AddRange(generated);
        }

        return segments;
    }

    private static List<ParticipantFlightSegmentEntity> CreateFlightSegmentsForParticipant(
        EventEntity entity,
        GeneratedParticipant generatedParticipant,
        int index,
        Random random,
        string flightLegMode)
    {
        var detail = generatedParticipant.Details;
        var participant = generatedParticipant.Participant;
        var multiLeg = ShouldUseLayover(index, flightLegMode);
        var segments = new List<ParticipantFlightSegmentEntity>(multiLeg ? 4 : 2);

        var arrivalOrigin = detail.ArrivalDepartureAirport ?? "IST";
        var arrivalDestination = detail.ArrivalArrivalAirport ?? "AYT";
        var arrivalDate = detail.ArrivalFlightDate ?? entity.StartDate.AddDays(-1);
        var arrivalDepartureTime = detail.ArrivalDepartureTime ?? new TimeOnly(8, 0);
        var arrivalArrivalTime = detail.ArrivalArrivalTime ?? AddMinutes(arrivalDepartureTime, 90);
        var arrivalCode = detail.ArrivalFlightCode ?? $"TFA{index:000}";

        if (multiLeg)
        {
            var connection = PickConnectionAirport(arrivalOrigin, arrivalDestination, index);
            var connectionArrival = AddMinutes(arrivalDepartureTime, 70);
            var connectionDeparture = AddMinutes(connectionArrival, 55);
            segments.Add(CreateFlightSegment(
                entity.OrganizationId,
                entity.Id,
                participant.Id,
                ParticipantFlightSegmentDirection.Arrival,
                1,
                detail.ArrivalAirline ?? "THY",
                arrivalOrigin,
                connection,
                $"{arrivalCode}A",
                arrivalDate,
                arrivalDepartureTime,
                arrivalDate,
                connectionArrival,
                detail.ArrivalPnr,
                detail.ArrivalTicketNo,
                detail.ArrivalBaggagePieces,
                detail.ArrivalBaggageTotalKg));
            segments.Add(CreateFlightSegment(
                entity.OrganizationId,
                entity.Id,
                participant.Id,
                ParticipantFlightSegmentDirection.Arrival,
                2,
                detail.ArrivalAirline ?? "THY",
                connection,
                arrivalDestination,
                $"{arrivalCode}B",
                arrivalDate,
                connectionDeparture,
                arrivalDate,
                arrivalArrivalTime,
                detail.ArrivalPnr,
                detail.ArrivalTicketNo,
                detail.ArrivalBaggagePieces,
                detail.ArrivalBaggageTotalKg));
        }
        else
        {
            segments.Add(CreateFlightSegment(
                entity.OrganizationId,
                entity.Id,
                participant.Id,
                ParticipantFlightSegmentDirection.Arrival,
                1,
                detail.ArrivalAirline ?? "THY",
                arrivalOrigin,
                arrivalDestination,
                arrivalCode,
                arrivalDate,
                arrivalDepartureTime,
                arrivalDate,
                arrivalArrivalTime,
                detail.ArrivalPnr,
                detail.ArrivalTicketNo,
                detail.ArrivalBaggagePieces,
                detail.ArrivalBaggageTotalKg));
        }

        var returnOrigin = detail.ReturnDepartureAirport ?? arrivalDestination;
        var returnDestination = detail.ReturnArrivalAirport ?? arrivalOrigin;
        var returnDate = detail.ReturnFlightDate ?? entity.EndDate;
        var returnDepartureTime = detail.ReturnDepartureTime ?? new TimeOnly(17, 0);
        var returnArrivalTime = detail.ReturnArrivalTime ?? AddMinutes(returnDepartureTime, 95);
        var returnCode = detail.ReturnFlightCode ?? $"TFR{index:000}";

        if (multiLeg)
        {
            var connection = PickConnectionAirport(returnOrigin, returnDestination, index + 11);
            var connectionArrival = AddMinutes(returnDepartureTime, 80);
            var connectionDeparture = AddMinutes(connectionArrival, 45);
            segments.Add(CreateFlightSegment(
                entity.OrganizationId,
                entity.Id,
                participant.Id,
                ParticipantFlightSegmentDirection.Return,
                1,
                detail.ReturnAirline ?? "Pegasus",
                returnOrigin,
                connection,
                $"{returnCode}A",
                returnDate,
                returnDepartureTime,
                returnDate,
                connectionArrival,
                detail.ReturnPnr,
                detail.ReturnTicketNo,
                detail.ReturnBaggagePieces,
                detail.ReturnBaggageTotalKg));
            segments.Add(CreateFlightSegment(
                entity.OrganizationId,
                entity.Id,
                participant.Id,
                ParticipantFlightSegmentDirection.Return,
                2,
                detail.ReturnAirline ?? "Pegasus",
                connection,
                returnDestination,
                $"{returnCode}B",
                returnDate,
                connectionDeparture,
                returnDate,
                returnArrivalTime,
                detail.ReturnPnr,
                detail.ReturnTicketNo,
                detail.ReturnBaggagePieces,
                detail.ReturnBaggageTotalKg));
        }
        else
        {
            segments.Add(CreateFlightSegment(
                entity.OrganizationId,
                entity.Id,
                participant.Id,
                ParticipantFlightSegmentDirection.Return,
                1,
                detail.ReturnAirline ?? "Pegasus",
                returnOrigin,
                returnDestination,
                returnCode,
                returnDate,
                returnDepartureTime,
                returnDate,
                returnArrivalTime,
                detail.ReturnPnr,
                detail.ReturnTicketNo,
                detail.ReturnBaggagePieces,
                detail.ReturnBaggageTotalKg));
        }

        return segments;
    }

    private static bool ShouldUseLayover(int participantIndex, string flightLegMode)
        => flightLegMode switch
        {
            "direct_only" => false,
            "layover_heavy" => participantIndex % 2 == 0,
            _ => participantIndex % 5 == 0,
        };

    private static List<ParticipantMealSelectionEntity> CreateMealSelections(
        EventEntity entity,
        IReadOnlyList<ActivityMealGroupEntity> mealGroups,
        IReadOnlyList<ParticipantEntity> participants,
        ResolvedScenarioEventRequest request,
        Random random,
        DateTime now)
    {
        if (mealGroups.Count == 0 || participants.Count == 0 || request.MealSelectionCoveragePercent <= 0)
        {
            return [];
        }

        var selectionCount = CalculateCoverageCount(participants.Count, request.MealSelectionCoveragePercent);
        var selectedParticipants = participants.Take(selectionCount).ToArray();
        var selections = new List<ParticipantMealSelectionEntity>(mealGroups.Count * selectedParticipants.Length);

        for (var groupIndex = 0; groupIndex < mealGroups.Count; groupIndex++)
        {
            var group = mealGroups[groupIndex];
            var options = group.Options.OrderBy(x => x.SortOrder).ToArray();

            for (var participantIndex = 0; participantIndex < selectedParticipants.Length; participantIndex++)
            {
                var participant = selectedParticipants[participantIndex];
                var useOther = group.AllowOther && participantIndex % 9 == 0;
                var includeNote = group.AllowNote && participantIndex % 7 == 0;
                var option = useOther || options.Length == 0
                    ? null
                    : options[(participantIndex + groupIndex) % options.Length];

                selections.Add(new ParticipantMealSelectionEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = entity.OrganizationId,
                    EventId = entity.Id,
                    ActivityId = group.ActivityId,
                    GroupId = group.Id,
                    ParticipantId = participant.Id,
                    OptionId = option?.Id,
                    OtherText = useOther ? (group.Title.Contains("Dinner", StringComparison.OrdinalIgnoreCase) ? "Vegan set menu" : "Gluten-free breakfast") : null,
                    Note = includeNote ? (random.Next(0, 2) == 0 ? "No nuts please." : "Lactose-free if possible.") : null,
                    CreatedAt = now.AddMinutes(participantIndex),
                    UpdatedAt = now.AddMinutes(participantIndex)
                });
            }
        }

        return selections;
    }

    private static List<CheckInEntity> CreateEventCheckIns(
        EventEntity entity,
        IReadOnlyList<ParticipantEntity> participants,
        ResolvedScenarioEventRequest request,
        Random random)
    {
        if (participants.Count == 0 || request.EventCheckInCoveragePercent <= 0)
        {
            return [];
        }

        var checkInCount = CalculateCoverageCount(participants.Count, request.EventCheckInCoveragePercent);
        var baseUtc = ConvertEventLocalToUtc(entity.StartDate, new TimeOnly(8, 0), request.TimeZoneId);
        var checkIns = new List<CheckInEntity>(checkInCount);

        for (var index = 0; index < checkInCount; index++)
        {
            checkIns.Add(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                EventId = entity.Id,
                ParticipantId = participants[index].Id,
                OrganizationId = entity.OrganizationId,
                CheckedInAt = baseUtc.AddMinutes(index * 3 + random.Next(0, 2)),
                Method = "manual"
            });
        }

        return checkIns;
    }

    private static EventPortalInfo BuildPortalInfo(
        EventEntity entity,
        IReadOnlyList<EventDayEntity> days,
        IReadOnlyList<EventActivityEntity> activities)
    {
        var baseInfo = EventsHelpers.CreateDefaultPortalInfo(EventsHelpers.ToDto(entity));
        var activitiesByDay = activities
            .GroupBy(x => x.EventDayId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.StartTime).ThenBy(y => y.Title).ToArray());

        var portalDays = days
            .OrderBy(x => x.SortOrder)
            .Select((day, index) =>
            {
                var items = activitiesByDay.TryGetValue(day.Id, out var dayActivities)
                    ? dayActivities
                        .Select(activity =>
                        {
                            var prefix = activity.StartTime.HasValue
                                ? $"{activity.StartTime.Value:HH\\:mm} · "
                                : string.Empty;
                            return $"{prefix}{activity.Title}";
                        })
                        .ToArray()
                    : [$"Arrival notes for day {index + 1}"];

                return new DayPlan(index + 1, day.Title ?? $"Day {index + 1}", items);
            })
            .ToArray();

        var meeting = new MeetingInfo(
            "08:30",
            $"{entity.Name} Hotel Lobby",
            "https://maps.google.com/?q=Tripflow+Demo+Hotel",
            $"Welcome to {entity.Name}. Please arrive 15 minutes early.");

        var links = new[]
        {
            new LinkInfo("Event guide", "https://example.com/tripflow/event-guide"),
            new LinkInfo("Support", "https://example.com/tripflow/support"),
            new LinkInfo("Feedback", "https://example.com/tripflow/feedback")
        };

        var notes = new[]
        {
            "Keep your phone charged for QR-based check-in.",
            "Share dietary restrictions before the meal deadline.",
            "Operations timings use the event time zone."
        };

        return new EventPortalInfo(meeting, links, portalDays, notes, EventsHelpers.ToEventContactsDto(entity));
    }

    private static (string FirstName, string LastName, string FullName) BuildParticipantName(
        ResolvedScenarioEventRequest request,
        Random random,
        int index)
    {
        if (request.ParticipantNamingMode == "prefix")
        {
            var prefix = request.ParticipantNamePrefix!.Trim();
            var firstName = prefix;
            var lastName = $"Guest {index:00}";
            return (firstName, lastName, $"{firstName} {lastName}");
        }

        var offset = request.RandomSeed.HasValue
            ? Math.Abs(request.RandomSeed.Value) % SampleNames.Length
            : random.Next(0, SampleNames.Length);
        var normalized = SampleNames[(offset + index - 1) % SampleNames.Length];
        SplitFullName(normalized, out var firstNameResult, out var lastNameResult);

        var cycle = (index - 1) / SampleNames.Length;
        if (cycle > 0)
        {
            lastNameResult = $"{lastNameResult} {cycle + 1}";
        }

        return (firstNameResult, lastNameResult, $"{firstNameResult} {lastNameResult}".Trim());
    }

    private static void SplitFullName(string fullName, out string firstName, out string lastName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            firstName = "Guest";
            lastName = "Demo";
            return;
        }

        if (parts.Length == 1)
        {
            firstName = parts[0];
            lastName = "Guest";
            return;
        }

        firstName = parts[0];
        lastName = string.Join(' ', parts.Skip(1));
    }

    private static string GenerateTcNo(int index, int? randomSeed)
    {
        var seedOffset = Math.Abs(randomSeed ?? 0) % 100000;
        return (10000000000L + (seedOffset * 1000L) + index).ToString(CultureInfo.InvariantCulture);
    }

    private static string GenerateCheckInCode(HashSet<string> existingCodes)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        while (true)
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            var code = new string(chars);
            if (existingCodes.Add(code))
            {
                return code;
            }
        }
    }

    private static ParticipantFlightSegmentEntity CreateFlightSegment(
        Guid organizationId,
        Guid eventId,
        Guid participantId,
        ParticipantFlightSegmentDirection direction,
        int segmentIndex,
        string airline,
        string departureAirport,
        string arrivalAirport,
        string flightCode,
        DateOnly departureDate,
        TimeOnly departureTime,
        DateOnly arrivalDate,
        TimeOnly arrivalTime,
        string? pnr,
        string? ticketNo,
        int? baggagePieces,
        int? baggageTotalKg)
    {
        return new ParticipantFlightSegmentEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EventId = eventId,
            ParticipantId = participantId,
            Direction = direction,
            SegmentIndex = segmentIndex,
            Airline = airline,
            DepartureAirport = departureAirport,
            ArrivalAirport = arrivalAirport,
            FlightCode = flightCode,
            DepartureDate = departureDate,
            DepartureTime = departureTime,
            ArrivalDate = arrivalDate,
            ArrivalTime = arrivalTime,
            Pnr = pnr,
            TicketNo = ticketNo,
            BaggagePieces = baggagePieces,
            BaggageTotalKg = baggageTotalKg
        };
    }

    private static string PickConnectionAirport(string departureAirport, string arrivalAirport, int seed)
    {
        for (var i = 0; i < Airports.Length; i++)
        {
            var candidate = Airports[(seed + i) % Airports.Length];
            if (!string.Equals(candidate, departureAirport, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate, arrivalAirport, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return "ESB";
    }

    private static int CalculateCoverageCount(int total, int percent)
    {
        if (total <= 0 || percent <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Round(total * (percent / 100d), MidpointRounding.AwayFromZero), 0, total);
    }

    private static bool IsMealActivity(string? type)
        => string.Equals(type?.Trim(), "Meal", StringComparison.OrdinalIgnoreCase);

    private static DateTime ConvertEventLocalToUtc(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        if (timeZone.IsInvalidTime(localDateTime))
        {
            localDateTime = localDateTime.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
    }

    private static TimeOnly AddMinutes(TimeOnly source, int minutes)
        => TimeOnly.FromDateTime(DateTime.UnixEpoch.Add(source.ToTimeSpan()).AddMinutes(minutes));

    private static string? NormalizeLower(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeNullableText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
