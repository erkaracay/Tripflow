using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;

namespace Tripflow.Api.Features.Events;

internal static class AccommodationSegmentAssignmentHelpers
{
    private const string DocTypeHotel = "hotel";

    internal static string? NormalizeOverwriteMode(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => "always",
            "always" => "always",
            "only_empty" => "only_empty",
            _ => null
        };
    }

    internal static string? NormalizeAccommodationMode(string? raw, string defaultValue = "keep")
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => defaultValue,
            "keep" when defaultValue == "keep" => "keep",
            "default" => "default",
            "override" => "override",
            _ => null
        };
    }

    internal static string? NormalizeFieldMode(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => "keep",
            "keep" => "keep",
            "set" => "set",
            "clear" => "clear",
            _ => null
        };
    }

    internal static async Task<(IResult? Error, Guid? DocTabId, string? Title)> ValidateHotelDocTab(
        Guid eventId,
        Guid organizationId,
        Guid? accommodationDocTabId,
        string fieldName,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!accommodationDocTabId.HasValue || accommodationDocTabId == Guid.Empty)
        {
            return (
                EventsHelpers.BadRequest(
                    "invalid_accommodation_doc_tab_id",
                    fieldName,
                    "Accommodation doc tab id must belong to this event and must be of type Hotel."),
                null,
                null);
        }

        var tab = await db.EventDocTabs.AsNoTracking()
            .Where(x =>
                x.Id == accommodationDocTabId.Value
                && x.EventId == eventId
                && x.OrganizationId == organizationId
                && x.Type != null
                && x.Type.ToLower() == DocTypeHotel)
            .Select(x => new { x.Id, x.Title })
            .FirstOrDefaultAsync(ct);
        if (tab is null)
        {
            return (
                EventsHelpers.BadRequest(
                    "invalid_accommodation_doc_tab_id",
                    fieldName,
                    "Accommodation doc tab id must belong to this event and must be of type Hotel."),
                null,
                null);
        }

        return (null, tab.Id, tab.Title);
    }

    internal static async Task<(IResult? Error, Guid? DocTabId)> ValidateOverrideMode(
        string accommodationMode,
        Guid? overrideAccommodationDocTabId,
        Guid eventId,
        Guid organizationId,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (accommodationMode != "override")
        {
            return (null, null);
        }

        var validation = await ValidateHotelDocTab(
            eventId,
            organizationId,
            overrideAccommodationDocTabId,
            "overrideAccommodationDocTabId",
            db,
            ct);
        return (validation.Error, validation.DocTabId);
    }

    internal static (IResult? Error, string? RoomNoValue, string? RoomTypeValue, string? BoardTypeValue, string? PersonNoValue) ValidateTextModes(
        string roomNoMode,
        string? roomNo,
        string roomTypeMode,
        string? roomType,
        string boardTypeMode,
        string? boardType,
        string personNoMode,
        string? personNo)
    {
        if (roomNoMode == "set" && NormalizeIncomingText(roomNo) is null)
        {
            return (EventsHelpers.BadRequest("invalid_room_no", "roomNo", "roomNo is required when roomNoMode is set."), null, null, null, null);
        }

        if (roomTypeMode == "set" && NormalizeIncomingText(roomType) is null)
        {
            return (EventsHelpers.BadRequest("invalid_room_type", "roomType", "roomType is required when roomTypeMode is set."), null, null, null, null);
        }

        if (boardTypeMode == "set" && NormalizeIncomingText(boardType) is null)
        {
            return (EventsHelpers.BadRequest("invalid_board_type", "boardType", "boardType is required when boardTypeMode is set."), null, null, null, null);
        }

        if (personNoMode == "set" && NormalizeIncomingText(personNo) is null)
        {
            return (EventsHelpers.BadRequest("invalid_person_no", "personNo", "personNo is required when personNoMode is set."), null, null, null, null);
        }

        return (
            null,
            NormalizeIncomingText(roomNo),
            NormalizeIncomingText(roomType),
            NormalizeIncomingText(boardType),
            NormalizeIncomingText(personNo));
    }

    internal static bool ApplyTextMode(
        string mode,
        string? incomingValue,
        bool onlyEmpty,
        ref string? targetValue)
    {
        var normalizedTarget = NormalizeStoredText(targetValue);
        switch (mode)
        {
            case "clear":
                if (normalizedTarget is null)
                {
                    return false;
                }

                targetValue = null;
                return true;
            case "set":
                if (onlyEmpty && normalizedTarget is not null)
                {
                    return false;
                }

                if (string.Equals(normalizedTarget, incomingValue, StringComparison.Ordinal))
                {
                    return false;
                }

                targetValue = incomingValue;
                return true;
            default:
                return false;
        }
    }

    internal static bool IsAssignmentEmpty(
        Guid? overrideAccommodationDocTabId,
        string? roomNo,
        string? roomType,
        string? boardType,
        string? personNo)
        => !overrideAccommodationDocTabId.HasValue
           && NormalizeStoredText(roomNo) is null
           && NormalizeStoredText(roomType) is null
           && NormalizeStoredText(boardType) is null
           && NormalizeStoredText(personNo) is null;

    internal static string? NormalizeIncomingText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    internal static string? NormalizeStoredText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
