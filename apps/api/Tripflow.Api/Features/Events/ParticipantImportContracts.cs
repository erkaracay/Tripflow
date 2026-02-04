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
    ParticipantImportPreviewRow[] ParsedPreviewRows);

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
    int? ReturnBaggageTotalKg);
