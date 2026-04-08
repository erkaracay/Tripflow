namespace Tripflow.Api.Features.Events;

public enum ParticipantImportMode
{
    Apply,
    DryRun
}

public sealed record ParticipantImportReport(
    int TotalRows,
    int ValidRows,
    int Imported,
    int Created,
    int Updated,
    int Skipped,
    int ErrorCount,
    int Failed,
    int PreviewLimit,
    bool PreviewTruncated,
    string[] IgnoredColumns,
    ParticipantImportError[] Errors,
    ParticipantImportWarning[] Warnings,
    ParticipantImportPreviewRow[] ParsedPreviewRows,
    int AccommodationSegmentsImported = 0,
    int AccommodationSegmentsCreated = 0,
    int AccommodationSegmentsUpdated = 0,
    int AccommodationAssignmentsImported = 0,
    int AccommodationAssignmentsCreated = 0,
    int AccommodationAssignmentsUpdated = 0,
    int AccommodationAssignmentsDeleted = 0);

public sealed record ParticipantImportError(int Row, string? TcNo, string Message, string[] Fields)
{
    public int RowIndex => Row;
    public string? Field { get; init; }
    public string? Code { get; init; }
}

public sealed record ParticipantImportWarning(int Row, string? TcNo, string Message, string Code)
{
    public int RowIndex => Row;
    public string? Field { get; init; }
}

public sealed record ParticipantImportPreviewRow(
    int RowIndex,
    string? FullName,
    string? ParticipantNameReference,
    string? Phone,
    string? TcNo,
    string? BirthDate,
    string? Gender,
    string? HotelCheckInDate,
    string? HotelCheckOutDate,
    string? ArrivalDepartureTime,
    string? ArrivalArrivalTime,
    string? ReturnDepartureTime,
    string? ReturnArrivalTime,
    int? ArrivalBaggagePieces,
    int? ArrivalBaggageTotalKg,
    int? ReturnBaggagePieces,
    int? ReturnBaggageTotalKg,
    string? RecordType = null,
    string? Direction = null,
    int? SegmentIndex = null,
    string? DepartureAirport = null,
    string? ArrivalAirport = null,
    string? FlightCode = null,
    string? DepartureDate = null,
    string? DepartureTime = null,
    string? ArrivalDate = null,
    string? ArrivalTime = null,
    string? CabinBaggage = null,
    string? SegmentKey = null,
    string? AccommodationTitle = null,
    string? StartDate = null,
    string? EndDate = null,
    string? RoomNo = null,
    string? RoomType = null,
    string? BoardType = null,
    string? PersonNo = null);
