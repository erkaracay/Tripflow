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
    string[] IgnoredColumns,
    ParticipantImportError[] Errors,
    ParticipantImportWarning[] Warnings);

public sealed record ParticipantImportError(int Row, string? TcNo, string Message, string[] Fields);
public sealed record ParticipantImportWarning(int Row, string? TcNo, string Message, string Code);
