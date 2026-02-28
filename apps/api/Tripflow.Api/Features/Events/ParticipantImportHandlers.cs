using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data;
using Tripflow.Api.Data.Entities;
using Tripflow.Api.Features.Organizations;

namespace Tripflow.Api.Features.Events;

internal static class ParticipantImportHandlers
{
    private const int MaxRows = 2000;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int PreviewRowLimit = 200;

    private static readonly string[] CanonicalHeaders =
    [
        "room_no",
        "room_type",
        "board_type",
        "person_no",
        "agency_name",
        "city",
        "first_name",
        "last_name",
        "full_name",
        "birth_date",
        "tc_no",
        "gender",
        "phone",
        "email",
        "flight_city",
        "hotel_check_in_date",
        "hotel_check_out_date",
        "arrival_ticket_no",
        "return_ticket_no",
        "insurance_company_name",
        "insurance_policy_no",
        "insurance_start_date",
        "insurance_end_date",
        "arrival_airline",
        "arrival_departure_airport",
        "arrival_arrival_airport",
        "arrival_flight_code",
        "arrival_flight_date",
        "arrival_departure_time",
        "arrival_arrival_time",
        "arrival_pnr",
        "arrival_baggage_pieces",
        "arrival_baggage_total_kg",
        "arrival_cabin_baggage",
        "return_airline",
        "return_departure_airport",
        "return_arrival_airport",
        "return_flight_code",
        "return_flight_date",
        "return_departure_time",
        "return_arrival_time",
        "return_pnr",
        "return_baggage_pieces",
        "return_baggage_total_kg",
        "return_cabin_baggage",
        "arrival_transfer_pickup_time",
        "arrival_transfer_pickup_place",
        "arrival_transfer_dropoff_place",
        "arrival_transfer_vehicle",
        "arrival_transfer_plate",
        "arrival_transfer_driver_info",
        "arrival_transfer_note",
        "return_transfer_pickup_time",
        "return_transfer_pickup_place",
        "return_transfer_dropoff_place",
        "return_transfer_vehicle",
        "return_transfer_plate",
        "return_transfer_driver_info",
        "return_transfer_note"
    ];

    private static readonly string[] ParticipantTemplateHeaders =
    [
        "room_no",
        "room_type",
        "board_type",
        "person_no",
        "agency_name",
        "city",
        "first_name",
        "last_name",
        "birth_date",
        "tc_no",
        "gender",
        "phone",
        "email",
        "flight_city",
        "hotel_check_in_date",
        "hotel_check_out_date",
        "insurance_company_name",
        "insurance_policy_no",
        "insurance_start_date",
        "insurance_end_date",
        "arrival_transfer_pickup_time",
        "arrival_transfer_pickup_place",
        "arrival_transfer_dropoff_place",
        "arrival_transfer_vehicle",
        "arrival_transfer_plate",
        "arrival_transfer_driver_info",
        "arrival_transfer_note",
        "return_transfer_pickup_time",
        "return_transfer_pickup_place",
        "return_transfer_dropoff_place",
        "return_transfer_vehicle",
        "return_transfer_plate",
        "return_transfer_driver_info",
        "return_transfer_note"
    ];

    private static readonly string[] ParticipantTemplateExampleRow =
    [
        "101",
        "Double",
        "HB",
        "2",
        "Sky Travel",
        "Istanbul",
        "Ayse",
        "Demir",
        "1990-05-10",
        "12345678901",
        "Female",
        "+905301112233",
        "ayse@example.com",
        "Istanbul",
        "2026-03-10",
        "2026-03-12",
        "Acme Insurance",
        "POL-123",
        "2026-03-10",
        "2026-03-12",
        "07:30",
        "Istanbul Airport",
        "Hotel Lobby",
        "Sprinter",
        "34 TF 123",
        "Driver Ali +90 555 222 33 44",
        "Meeting at gate 5",
        "18:00",
        "Hotel Lobby",
        "Istanbul Airport",
        "Vito",
        "34 TF 987",
        "Driver Ayse +90 555 666 77 88",
        "Pickup 15 min earlier"
    ];

    private static readonly string[] FlightSegmentRequiredHeaders =
    [
        "tc_no",
        "direction",
        "segment_index",
        "departure_airport",
        "arrival_airport",
        "flight_code",
        "departure_date",
        "departure_time"
    ];

    private static readonly string[] FlightSegmentOptionalHeaders =
    [
        "participant_name",
        "airline",
        "arrival_date",
        "arrival_time",
        "pnr",
        "ticket_no",
        "baggage_pieces",
        "baggage_total_kg",
        "cabin_baggage"
    ];

    private static readonly string[] FlightSegmentCanonicalHeaders =
    [
        ..FlightSegmentRequiredHeaders,
        ..FlightSegmentOptionalHeaders
    ];

    private static readonly string[] LegacyParticipantFlightHeaders =
    [
        "arrival_ticket_no",
        "return_ticket_no",
        "arrival_airline",
        "arrival_departure_airport",
        "arrival_arrival_airport",
        "arrival_flight_code",
        "arrival_flight_date",
        "arrival_departure_time",
        "arrival_arrival_time",
        "arrival_pnr",
        "arrival_baggage_allowance",
        "arrival_baggage_pieces",
        "arrival_baggage_total_kg",
        "arrival_cabin_baggage",
        "return_airline",
        "return_departure_airport",
        "return_arrival_airport",
        "return_flight_code",
        "return_flight_date",
        "return_departure_time",
        "return_arrival_time",
        "return_pnr",
        "return_baggage_allowance",
        "return_baggage_pieces",
        "return_baggage_total_kg",
        "return_cabin_baggage"
    ];

    private static readonly string[] FlightSegmentExampleRows1 =
    [
        "12345678901",
        "Arrival",
        "1",
        "IST",
        "ADB",
        "TK2304",
        "2026-03-10",
        "08:15",
        "Ayse Demir",
        "Turkish Airlines",
        "2026-03-10",
        "09:35",
        "PNR-ARR-1",
        "TK-123-OUT",
        "1",
        "23",
        "8 kg + personal item"
    ];

    private static readonly string[] FlightSegmentExampleRows2 =
    [
        "12345678901",
        "Arrival",
        "2",
        "ADB",
        "ASR",
        "TK2378",
        "2026-03-10",
        "11:20",
        "Ayse Demir",
        "Turkish Airlines",
        "2026-03-10",
        "12:35",
        "PNR-ARR-2",
        "TK-123-OUT",
        "1",
        "23",
        "1 cabin suitcase"
    ];

    private static readonly string[] FlightSegmentExampleRows3 =
    [
        "12345678901",
        "Return",
        "1",
        "ASR",
        "ADB",
        "TK2379",
        "2026-03-14",
        "16:10",
        "Ayse Demir",
        "Turkish Airlines",
        "2026-03-14",
        "17:25",
        "PNR-RET-1",
        "TK-456-RET",
        "1",
        "23",
        "8 kg"
    ];

    private static readonly string[] FlightSegmentExampleRows4 =
    [
        "12345678901",
        "Return",
        "2",
        "ADB",
        "IST",
        "TK2305",
        "2026-03-14",
        "19:05",
        "Ayse Demir",
        "Turkish Airlines",
        "2026-03-14",
        "20:20",
        "PNR-RET-2",
        "TK-456-RET",
        "1",
        "23",
        "8 kg + backpack"
    ];

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yy"];
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss", "HH.mm", "H:mm"];

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "birth_date",
        "hotel_check_in_date",
        "hotel_check_out_date",
        "insurance_start_date",
        "insurance_end_date",
        "arrival_flight_date",
        "return_flight_date",
        "departure_date",
        "arrival_date"
    };

    private static readonly HashSet<string> TimeColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "arrival_departure_time",
        "arrival_arrival_time",
        "return_departure_time",
        "return_arrival_time",
        "arrival_transfer_pickup_time",
        "return_transfer_pickup_time",
        "departure_time",
        "arrival_time"
    };

    private static readonly Regex BaggagePiecesRegex =
        new(@"(?<value>\d+(?:[.,]\d+)?)\s*(pc|pcs|piece|pieces|parca)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BaggageKgRegex =
        new(@"(?<value>\d+(?:[.,]\d+)?)\s*(kg|kgs|kilo|kilogram|kilograms)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static async Task<IResult> DownloadImportTemplate(
        string eventId,
        string? format,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventExists = await db.Events.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (!eventExists)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        var formatValue = (format ?? "csv").Trim().ToLowerInvariant();
        return formatValue switch
        {
            "csv" => Results.File(BuildCsvTemplate(), "text/csv", "participants_template.csv"),
            "xlsx" => Results.File(BuildXlsxTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "participants_template.xlsx"),
            _ => EventsHelpers.BadRequest("Format must be csv or xlsx.")
        };
    }

    internal static async Task<IResult> ImportParticipants(
        string eventId,
        string? mode,
        IFormFile? file,
        HttpContext httpContext,
        TripflowDbContext db,
        CancellationToken ct)
    {
        if (!EventsHelpers.TryParseEventId(eventId, out var id, out var error))
        {
            return error!;
        }

        if (!OrganizationHelpers.TryResolveOrganizationId(httpContext, out var orgId, out var orgError))
        {
            return orgError!;
        }

        var eventEntity = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (eventEntity is null)
        {
            return Results.NotFound(new { message = "Event not found." });
        }

        if (file is null || file.Length == 0)
        {
            return EventsHelpers.BadRequest("File is required.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return EventsHelpers.BadRequest("File too large. Max size is 10MB.");
        }

        var modeValue = (mode ?? "apply").Trim().ToLowerInvariant();
        if (modeValue is not "apply" and not "dryrun")
        {
            return EventsHelpers.BadRequest("Mode must be apply or dryrun.");
        }

        var importMode = modeValue == "dryrun"
            ? ParticipantImportMode.DryRun
            : ParticipantImportMode.Apply;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx")
        {
            return EventsHelpers.BadRequest("Only .csv and .xlsx files are supported.");
        }

        ImportPayload payload;
        using (var stream = file.OpenReadStream())
        {
            try
            {
                payload = extension == ".csv"
                    ? ReadCsv(stream)
                    : ReadXlsx(stream);
            }
            catch (Exception)
            {
                return EventsHelpers.BadRequest("Invalid file format. Please upload a valid CSV or XLSX.");
            }
        }

        if (payload.Rows.Count > MaxRows)
        {
            return EventsHelpers.BadRequest($"Max {MaxRows} rows allowed.");
        }

        var errors = new List<ParticipantImportError>();
        var warnings = new List<ParticipantImportWarning>();
        var previewRows = new List<ParticipantImportPreviewRow>();

        if (payload.LegacyHeadersDetected)
        {
            warnings.Add(new ParticipantImportWarning(
                1,
                null,
                "Legacy template headers detected. Please use the latest import template.",
                "legacy_template_detected"));
        }

        if (payload.FlightSegmentLegacyHeadersDetected)
        {
            warnings.Add(new ParticipantImportWarning(
                1,
                null,
                "Legacy headers detected in flight_segments sheet. Please use the latest import template.",
                "legacy_template_detected")
            {
                Field = "flight_segments"
            });
        }

        if (payload.MissingRequiredColumns.Length > 0)
        {
            errors.Add(new ParticipantImportError(
                1,
                null,
                $"Missing required columns: {string.Join(", ", payload.MissingRequiredColumns)}",
                payload.MissingRequiredColumns)
            {
                Code = "missing_required_columns",
                Field = payload.MissingRequiredColumns.Length == 1 ? payload.MissingRequiredColumns[0] : "participants"
            });

            var missingColumnsReport = BuildReport(payload, 0, 0, 0, errors, warnings, previewRows);
            return Results.BadRequest(missingColumnsReport);
        }

        if (payload.MissingFlightSegmentRequiredColumns.Length > 0)
        {
            errors.Add(new ParticipantImportError(
                1,
                null,
                $"flight_segments sheet missing required columns: {string.Join(", ", payload.MissingFlightSegmentRequiredColumns)}",
                payload.MissingFlightSegmentRequiredColumns)
            {
                Code = "missing_required_columns",
                Field = "flight_segments"
            });

            var missingColumnsReport = BuildReport(payload, 0, 0, 0, errors, warnings, previewRows);
            return Results.BadRequest(missingColumnsReport);
        }

        var fileTcRows = BuildFileTcRowMap(payload.Rows);
        var duplicateTcInFile = fileTcRows
            .Where(x => x.Value.Count > 1)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        foreach (var duplicate in duplicateTcInFile)
        {
            foreach (var rowNo in duplicate.Value)
            {
                errors.Add(new ParticipantImportError(
                    rowNo,
                    duplicate.Key,
                    "Duplicate tcNo in file.",
                    ["tc_no"])
                {
                    Code = "duplicate_tcno_file",
                    Field = "tc_no"
                });
            }
        }

        var blockedRows = new HashSet<int>(errors.Select(x => x.Row));

        var existingParticipants = await db.Participants
            .Include(x => x.Details)
            .Where(x => x.EventId == id && x.OrganizationId == orgId)
            .ToListAsync(ct);

        var existingByTc = existingParticipants
            .GroupBy(x => NormalizeTcNo(x.TcNo), StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var ambiguousDbTc = existingByTc
            .Where(x => x.Value.Count > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        var usedCodes = new HashSet<string>(
            existingParticipants.Select(x => x.CheckInCode),
            StringComparer.Ordinal);

        var createdParticipants = new List<ParticipantEntity>();
        var imported = 0;
        var created = 0;
        var updated = 0;
        var now = DateTime.UtcNow;
        await using var transaction = importMode == ParticipantImportMode.Apply
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var hasFlightSegmentRows = payload.FlightSegmentRows.Count > 0;
        var ignoreLegacyParticipantFlights = hasFlightSegmentRows;

        if (ignoreLegacyParticipantFlights && HasAnyLegacyParticipantFlightValue(payload.Rows))
        {
            warnings.Add(new ParticipantImportWarning(
                1,
                null,
                "participants sheet legacy flight columns are ignored when flight_segments sheet has rows.",
                "participants_flight_columns_ignored_due_segments_sheet")
            {
                Field = "flight_segments"
            });
        }

        try
        {
            foreach (var row in payload.Rows)
            {
                if (blockedRows.Contains(row.RowNumber))
                {
                    continue;
                }

                var tcNoRaw = row.GetValue("tc_no");
                var tcNo = NormalizeTcNo(tcNoRaw);

                var firstName = NormalizeName(row.GetValue("first_name"));
                var lastName = NormalizeName(row.GetValue("last_name"));
                var legacyFullName = NormalizeName(row.GetValue("full_name"));

                if ((!string.IsNullOrWhiteSpace(legacyFullName))
                    && (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)))
                {
                    SplitFullName(legacyFullName, out var parsedFirstName, out var parsedLastName);
                    firstName = parsedFirstName;
                    lastName = parsedLastName;
                }

                var fullName = BuildFullName(firstName, lastName);

                var phone = NormalizePhone(row.GetValue("phone"));
                var email = NormalizeEmail(row.GetValue("email"));
                var genderRaw = row.GetValue("gender");

                var rowErrors = new List<string>();
                var errorFields = new List<string>();
                var tcNoForIssues = string.IsNullOrWhiteSpace(tcNo) ? null : tcNo;

                if (string.IsNullOrWhiteSpace(firstName))
                {
                    rowErrors.Add("first_name required");
                    errorFields.Add("first_name");
                }

                if (string.IsNullOrWhiteSpace(lastName))
                {
                    rowErrors.Add("last_name required");
                    errorFields.Add("last_name");
                }

                if (string.IsNullOrWhiteSpace(phone))
                {
                    rowErrors.Add("phone required");
                    errorFields.Add("phone");
                }

                if (string.IsNullOrWhiteSpace(tcNo) || tcNo.Length != 11)
                {
                    rowErrors.Add("tc_no must be 11 digits");
                    errorFields.Add("tc_no");
                }

                var birthDateValid = TryParseDate(row.GetValue("birth_date"), out var birthDate);
                if (!birthDateValid)
                {
                    rowErrors.Add("birth_date invalid");
                    errorFields.Add("birth_date");
                }

                var genderValid = TryParseGender(genderRaw, out var gender);
                if (!genderValid)
                {
                    rowErrors.Add("gender invalid");
                    errorFields.Add("gender");
                }

                if (!TryParseOptionalDate(row.GetValue("hotel_check_in_date"), out var hotelCheckIn))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "hotel_check_in_date invalid",
                        "invalid_date")
                    {
                        Field = "hotel_check_in_date"
                    });
                }

                if (!TryParseOptionalDate(row.GetValue("hotel_check_out_date"), out var hotelCheckOut))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "hotel_check_out_date invalid",
                        "invalid_date")
                    {
                        Field = "hotel_check_out_date"
                    });
                }

                if (!TryParseOptionalDate(row.GetValue("insurance_start_date"), out var insuranceStart))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "insurance_start_date invalid",
                        "invalid_date")
                    {
                        Field = "insurance_start_date"
                    });
                }

                if (!TryParseOptionalDate(row.GetValue("insurance_end_date"), out var insuranceEnd))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "insurance_end_date invalid",
                        "invalid_date")
                    {
                        Field = "insurance_end_date"
                    });
                }

                DateOnly? arrivalFlightDate = null;
                DateOnly? returnFlightDate = null;
                TimeOnly? arrivalDepartureTime = null;
                TimeOnly? arrivalArrivalTime = null;
                TimeOnly? returnDepartureTime = null;
                TimeOnly? returnArrivalTime = null;
                int? arrivalBaggagePieces = null;
                int? arrivalBaggageTotalKg = null;
                int? returnBaggagePieces = null;
                int? returnBaggageTotalKg = null;

                if (!ignoreLegacyParticipantFlights)
                {
                    if (!TryParseOptionalDate(row.GetValue("arrival_flight_date"), out arrivalFlightDate))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "arrival_flight_date invalid",
                            "invalid_date")
                        {
                            Field = "arrival_flight_date"
                        });
                    }

                    if (!TryParseOptionalDate(row.GetValue("return_flight_date"), out returnFlightDate))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "return_flight_date invalid",
                            "invalid_date")
                        {
                            Field = "return_flight_date"
                        });
                    }

                    if (!TryParseOptionalTime(row.GetValue("arrival_departure_time"), out arrivalDepartureTime))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "arrival_departure_time invalid",
                            "invalid_time")
                        {
                            Field = "arrival_departure_time"
                        });
                    }

                    if (!TryParseOptionalTime(row.GetValue("arrival_arrival_time"), out arrivalArrivalTime))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "arrival_arrival_time invalid",
                            "invalid_time")
                        {
                            Field = "arrival_arrival_time"
                        });
                    }

                    if (!TryParseOptionalTime(row.GetValue("return_departure_time"), out returnDepartureTime))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "return_departure_time invalid",
                            "invalid_time")
                        {
                            Field = "return_departure_time"
                        });
                    }

                    if (!TryParseOptionalTime(row.GetValue("return_arrival_time"), out returnArrivalTime))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "return_arrival_time invalid",
                            "invalid_time")
                        {
                            Field = "return_arrival_time"
                        });
                    }
                }

                if (!TryParseOptionalTime(row.GetValue("arrival_transfer_pickup_time"), out var arrivalTransferPickupTime))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "arrival_transfer_pickup_time invalid",
                        "invalid_time")
                    {
                        Field = "arrival_transfer_pickup_time"
                    });
                }

                if (!TryParseOptionalTime(row.GetValue("return_transfer_pickup_time"), out var returnTransferPickupTime))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "return_transfer_pickup_time invalid",
                        "invalid_time")
                    {
                        Field = "return_transfer_pickup_time"
                    });
                }

                if (!ignoreLegacyParticipantFlights)
                {
                    if (!TryParseOptionalBaggagePieces(
                            row.GetValue("arrival_baggage_pieces"),
                            out arrivalBaggagePieces,
                            out _))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "arrival_baggage_pieces invalid",
                            "invalid_baggage_pieces")
                        {
                            Field = "arrival_baggage_pieces"
                        });
                    }

                    if (!TryParseOptionalBaggageTotalKg(
                            row.GetValue("arrival_baggage_total_kg"),
                            out arrivalBaggageTotalKg,
                            out var arrivalKgRounded))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "arrival_baggage_total_kg invalid",
                            "invalid_baggage_total_kg")
                        {
                            Field = "arrival_baggage_total_kg"
                        });
                    }
                    else if (arrivalKgRounded)
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "arrival_baggage_total_kg decimal rounded to nearest integer",
                            "baggage_total_kg_rounded")
                        {
                            Field = "arrival_baggage_total_kg"
                        });
                    }

                    if (!TryParseOptionalBaggagePieces(
                            row.GetValue("return_baggage_pieces"),
                            out returnBaggagePieces,
                            out _))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "return_baggage_pieces invalid",
                            "invalid_baggage_pieces")
                        {
                            Field = "return_baggage_pieces"
                        });
                    }

                    if (!TryParseOptionalBaggageTotalKg(
                            row.GetValue("return_baggage_total_kg"),
                            out returnBaggageTotalKg,
                            out var returnKgRounded))
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "return_baggage_total_kg invalid",
                            "invalid_baggage_total_kg")
                        {
                            Field = "return_baggage_total_kg"
                        });
                    }
                    else if (returnKgRounded)
                    {
                        warnings.Add(new ParticipantImportWarning(
                            row.RowNumber,
                            tcNoForIssues,
                            "return_baggage_total_kg decimal rounded to nearest integer",
                            "baggage_total_kg_rounded")
                        {
                            Field = "return_baggage_total_kg"
                        });
                    }

                    var arrivalBaggageAllowance = row.GetValue("arrival_baggage_allowance");
                    if ((!arrivalBaggagePieces.HasValue || !arrivalBaggageTotalKg.HasValue)
                        && !string.IsNullOrWhiteSpace(arrivalBaggageAllowance))
                    {
                        var needsPieces = !arrivalBaggagePieces.HasValue;
                        var needsKg = !arrivalBaggageTotalKg.HasValue;
                        var allowance = TryParseBaggageAllowance(arrivalBaggageAllowance);

                        if (needsPieces && allowance.Pieces.HasValue)
                        {
                            arrivalBaggagePieces = allowance.Pieces;
                        }

                        if (needsKg && allowance.TotalKg.HasValue)
                        {
                            arrivalBaggageTotalKg = allowance.TotalKg;
                        }

                        if (needsPieces && (allowance.PiecesDecimal || allowance.PiecesInvalid))
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "arrival_baggage_allowance contains invalid piece value",
                                "invalid_baggage_pieces")
                            {
                                Field = "arrival_baggage_allowance"
                            });
                        }

                        if (needsKg && allowance.KgInvalid)
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "arrival_baggage_allowance contains invalid kg value",
                                "invalid_baggage_total_kg")
                            {
                                Field = "arrival_baggage_allowance"
                            });
                        }

                        if (needsKg && allowance.KgRounded)
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "arrival_baggage_total_kg decimal rounded to nearest integer",
                                "baggage_total_kg_rounded")
                            {
                                Field = "arrival_baggage_total_kg"
                            });
                        }

                        if (needsPieces && needsKg && allowance.Pieces is null && allowance.TotalKg is null)
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "arrival_baggage_allowance could not be parsed",
                                "invalid_baggage_allowance")
                            {
                                Field = "arrival_baggage_allowance"
                            });
                        }
                    }

                    var returnBaggageAllowance = row.GetValue("return_baggage_allowance");
                    if ((!returnBaggagePieces.HasValue || !returnBaggageTotalKg.HasValue)
                        && !string.IsNullOrWhiteSpace(returnBaggageAllowance))
                    {
                        var needsPieces = !returnBaggagePieces.HasValue;
                        var needsKg = !returnBaggageTotalKg.HasValue;
                        var allowance = TryParseBaggageAllowance(returnBaggageAllowance);

                        if (needsPieces && allowance.Pieces.HasValue)
                        {
                            returnBaggagePieces = allowance.Pieces;
                        }

                        if (needsKg && allowance.TotalKg.HasValue)
                        {
                            returnBaggageTotalKg = allowance.TotalKg;
                        }

                        if (needsPieces && (allowance.PiecesDecimal || allowance.PiecesInvalid))
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "return_baggage_allowance contains invalid piece value",
                                "invalid_baggage_pieces")
                            {
                                Field = "return_baggage_allowance"
                            });
                        }

                        if (needsKg && allowance.KgInvalid)
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "return_baggage_allowance contains invalid kg value",
                                "invalid_baggage_total_kg")
                            {
                                Field = "return_baggage_allowance"
                            });
                        }

                        if (needsKg && allowance.KgRounded)
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "return_baggage_total_kg decimal rounded to nearest integer",
                                "baggage_total_kg_rounded")
                            {
                                Field = "return_baggage_total_kg"
                            });
                        }

                        if (needsPieces && needsKg && allowance.Pieces is null && allowance.TotalKg is null)
                        {
                            warnings.Add(new ParticipantImportWarning(
                                row.RowNumber,
                                tcNoForIssues,
                                "return_baggage_allowance could not be parsed",
                                "invalid_baggage_allowance")
                            {
                                Field = "return_baggage_allowance"
                            });
                        }
                    }
                }

                if (previewRows.Count < PreviewRowLimit)
                {
                    previewRows.Add(new ParticipantImportPreviewRow(
                        row.RowNumber,
                        string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                        null,
                        string.IsNullOrWhiteSpace(phone) ? null : phone,
                        string.IsNullOrWhiteSpace(tcNo) ? null : tcNo,
                        birthDateValid ? birthDate.ToString("yyyy-MM-dd") : null,
                        genderValid ? gender.ToString() : null,
                        hotelCheckIn?.ToString("yyyy-MM-dd"),
                        hotelCheckOut?.ToString("yyyy-MM-dd"),
                        arrivalDepartureTime?.ToString("HH:mm"),
                        arrivalArrivalTime?.ToString("HH:mm"),
                        returnDepartureTime?.ToString("HH:mm"),
                        returnArrivalTime?.ToString("HH:mm"),
                        arrivalBaggagePieces,
                        arrivalBaggageTotalKg,
                        returnBaggagePieces,
                        returnBaggageTotalKg));
                }

                if (rowErrors.Count > 0)
                {
                    var primaryField = errorFields.Count == 1 ? errorFields[0] : null;
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        tcNoForIssues,
                        string.Join("; ", rowErrors),
                        errorFields.ToArray())
                    {
                        Field = primaryField,
                        Code = primaryField is null ? "invalid_required_fields" : $"invalid_{primaryField}"
                    });
                    continue;
                }

                if (ambiguousDbTc.Contains(tcNo))
                {
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        tcNo,
                        "tc_no matches multiple existing participants in this event.",
                        ["tc_no"])
                    {
                        Code = "tcno_ambiguous",
                        Field = "tc_no"
                    });
                    continue;
                }

                var hasDetails = row.HasAnyDetails || hotelCheckIn is not null || hotelCheckOut is not null
                    || arrivalDepartureTime is not null || arrivalArrivalTime is not null
                    || returnDepartureTime is not null || returnArrivalTime is not null
                    || arrivalTransferPickupTime is not null || returnTransferPickupTime is not null;

                if (existingByTc.TryGetValue(tcNo, out var matches) && matches.Count == 1)
                {
                    updated++;
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNo,
                        "tc_no already exists in event; row will update existing participant.",
                        "duplicate_tcno")
                    {
                        Field = "tc_no"
                    });

                    if (importMode == ParticipantImportMode.Apply)
                    {
                        var participant = matches[0];
                        participant.FirstName = firstName;
                        participant.LastName = lastName;
                        participant.FullName = fullName;
                        participant.Phone = phone;
                        participant.Email = email;
                        participant.TcNo = tcNo;
                        participant.BirthDate = birthDate;
                        participant.Gender = gender;

                        if (hasDetails)
                        {
                            participant.Details ??= new ParticipantDetailsEntity { ParticipantId = participant.Id };
                            ApplyDetails(participant.Details, row, hotelCheckIn, hotelCheckOut, insuranceStart,
                                insuranceEnd, arrivalFlightDate, returnFlightDate, arrivalDepartureTime, arrivalArrivalTime, returnDepartureTime,
                                returnArrivalTime,
                                arrivalBaggagePieces, arrivalBaggageTotalKg, returnBaggagePieces, returnBaggageTotalKg,
                                arrivalTransferPickupTime, returnTransferPickupTime, !ignoreLegacyParticipantFlights);
                        }
                        else if (participant.Details is not null)
                        {
                            participant.Details = null;
                        }
                    }
                }
                else
                {
                    created++;
                    if (importMode == ParticipantImportMode.Apply)
                    {
                        var newParticipant = new ParticipantEntity
                        {
                            Id = Guid.NewGuid(),
                            EventId = id,
                            OrganizationId = orgId,
                            FirstName = firstName,
                            LastName = lastName,
                            FullName = fullName,
                            Phone = phone,
                            Email = email,
                            TcNo = tcNo,
                            BirthDate = birthDate,
                            Gender = gender,
                            CheckInCode = GenerateUniqueCheckInCode(usedCodes),
                            CreatedAt = now
                        };

                        if (hasDetails)
                        {
                            var details = new ParticipantDetailsEntity
                            {
                                ParticipantId = newParticipant.Id
                            };
                            ApplyDetails(details, row, hotelCheckIn, hotelCheckOut, insuranceStart, insuranceEnd,
                                arrivalFlightDate, returnFlightDate, arrivalDepartureTime, arrivalArrivalTime, returnDepartureTime, returnArrivalTime,
                                arrivalBaggagePieces, arrivalBaggageTotalKg, returnBaggagePieces, returnBaggageTotalKg,
                                arrivalTransferPickupTime, returnTransferPickupTime, !ignoreLegacyParticipantFlights);
                            newParticipant.Details = details;
                        }

                        createdParticipants.Add(newParticipant);
                        existingByTc[tcNo] = [newParticipant];
                    }
                    else
                    {
                        existingByTc[tcNo] = [new ParticipantEntity
                        {
                            Id = Guid.NewGuid(),
                            TcNo = tcNo,
                            FullName = fullName
                        }];
                    }
                }

                imported++;
            }

            if (importMode == ParticipantImportMode.Apply && createdParticipants.Count > 0)
            {
                db.Participants.AddRange(createdParticipants);
                createdParticipants.Clear();
            }

            var parsedFlightSegmentRows = new List<ParsedFlightSegmentRow>();

            foreach (var row in payload.FlightSegmentRows)
            {
                var tcNo = NormalizeTcNo(row.GetValue("tc_no"));
                var tcNoForIssues = string.IsNullOrWhiteSpace(tcNo) ? null : tcNo;
                var participantNameReference = NormalizeOptionalText(row.GetValue("participant_name"));
                var rowErrors = new List<string>();
                var rowFields = new List<string>();

                if (string.IsNullOrWhiteSpace(tcNo) || tcNo.Length != 11)
                {
                    rowErrors.Add("tc_no must be 11 digits");
                    rowFields.Add("tc_no");
                }

                if (!TryParseFlightSegmentDirection(row.GetValue("direction"), out var direction))
                {
                    rowErrors.Add("direction must be Arrival, Return, Gidiş or Dönüş");
                    rowFields.Add("direction");
                }

                if (!TryParseSegmentIndex(row.GetValue("segment_index"), out var segmentIndex))
                {
                    rowErrors.Add("segment_index must be an integer greater than 0");
                    rowFields.Add("segment_index");
                }

                var departureAirport = NormalizeOptionalText(row.GetValue("departure_airport"));
                if (string.IsNullOrWhiteSpace(departureAirport))
                {
                    rowErrors.Add("departure_airport required");
                    rowFields.Add("departure_airport");
                }

                var arrivalAirport = NormalizeOptionalText(row.GetValue("arrival_airport"));
                if (string.IsNullOrWhiteSpace(arrivalAirport))
                {
                    rowErrors.Add("arrival_airport required");
                    rowFields.Add("arrival_airport");
                }

                var flightCode = NormalizeOptionalText(row.GetValue("flight_code"));
                if (string.IsNullOrWhiteSpace(flightCode))
                {
                    rowErrors.Add("flight_code required");
                    rowFields.Add("flight_code");
                }

                if (!TryParseOptionalDate(row.GetValue("departure_date"), out var departureDate) || departureDate is null)
                {
                    rowErrors.Add("departure_date invalid");
                    rowFields.Add("departure_date");
                }

                if (!TryParseOptionalTime(row.GetValue("departure_time"), out var departureTime) || departureTime is null)
                {
                    rowErrors.Add("departure_time invalid");
                    rowFields.Add("departure_time");
                }

                if (!TryParseOptionalDate(row.GetValue("arrival_date"), out var arrivalDate))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "arrival_date invalid",
                        "invalid_date")
                    {
                        Field = "arrival_date"
                    });
                }

                if (!TryParseOptionalTime(row.GetValue("arrival_time"), out var arrivalTime))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "arrival_time invalid",
                        "invalid_time")
                    {
                        Field = "arrival_time"
                    });
                }

                if (!TryParseOptionalBaggagePieces(row.GetValue("baggage_pieces"), out var baggagePieces, out var baggagePiecesDecimal))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        baggagePiecesDecimal
                            ? "baggage_pieces must be a whole number"
                            : "baggage_pieces invalid",
                        baggagePiecesDecimal ? "baggage_pieces_decimal_not_allowed" : "invalid_baggage_pieces")
                    {
                        Field = "baggage_pieces"
                    });
                }

                if (!TryParseOptionalBaggageTotalKg(row.GetValue("baggage_total_kg"), out var baggageTotalKg, out var baggageRounded))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "baggage_total_kg invalid",
                        "invalid_baggage_total_kg")
                    {
                        Field = "baggage_total_kg"
                    });
                }
                else if (baggageRounded)
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNoForIssues,
                        "baggage_total_kg decimal rounded to nearest integer",
                        "baggage_total_kg_rounded")
                    {
                        Field = "baggage_total_kg"
                    });
                }

                if (rowErrors.Count > 0)
                {
                    var primaryField = rowFields.Count == 1 ? rowFields[0] : null;
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        tcNoForIssues,
                        string.Join("; ", rowErrors),
                        rowFields.ToArray())
                    {
                        Field = primaryField,
                        Code = primaryField is null ? "invalid_required_fields" : $"invalid_{primaryField}"
                    });
                    continue;
                }

                if (!existingByTc.TryGetValue(tcNo, out var matches) || matches.Count == 0)
                {
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        tcNo,
                        "tc_no not found in this event.",
                        ["tc_no"])
                    {
                        Field = "tc_no",
                        Code = "tcno_not_found"
                    });
                    continue;
                }

                if (matches.Count > 1)
                {
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        tcNo,
                        "tc_no matches multiple participants in this event.",
                        ["tc_no"])
                    {
                        Field = "tc_no",
                        Code = "tcno_ambiguous"
                    });
                    continue;
                }

                var matchedFullName = string.IsNullOrWhiteSpace(matches[0].FullName) ? null : matches[0].FullName;
                if (!string.IsNullOrWhiteSpace(participantNameReference)
                    && !NamesMatchForReference(participantNameReference, matchedFullName))
                {
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNo,
                        "participant_name does not match the participant resolved by tc_no.",
                        "participant_name_mismatch_for_tc_no")
                    {
                        Field = "participant_name"
                    });
                }

                var cabinBaggage = NormalizeOptionalText(row.GetValue("cabin_baggage"));
                var participantId = matches[0].Id;
                parsedFlightSegmentRows.Add(new ParsedFlightSegmentRow(
                    row.RowNumber,
                    participantId,
                    direction,
                    segmentIndex,
                    NormalizeOptionalText(row.GetValue("airline")),
                    departureAirport,
                    arrivalAirport,
                    flightCode,
                    departureDate,
                    departureTime,
                    arrivalDate,
                    arrivalTime,
                    NormalizeOptionalText(row.GetValue("pnr")),
                    NormalizeOptionalText(row.GetValue("ticket_no")),
                    baggagePieces,
                    baggageTotalKg,
                    cabinBaggage,
                    participantNameReference,
                    tcNo));

                if (previewRows.Count < PreviewRowLimit)
                {
                    previewRows.Add(new ParticipantImportPreviewRow(
                        row.RowNumber,
                        matchedFullName,
                        participantNameReference,
                        null,
                        tcNo,
                        null,
                        null,
                        null,
                        null,
                        direction == ParticipantFlightSegmentDirection.Arrival ? departureTime?.ToString("HH:mm") : null,
                        direction == ParticipantFlightSegmentDirection.Arrival ? arrivalTime?.ToString("HH:mm") : null,
                        direction == ParticipantFlightSegmentDirection.Return ? departureTime?.ToString("HH:mm") : null,
                        direction == ParticipantFlightSegmentDirection.Return ? arrivalTime?.ToString("HH:mm") : null,
                        direction == ParticipantFlightSegmentDirection.Arrival ? baggagePieces : null,
                        direction == ParticipantFlightSegmentDirection.Arrival ? baggageTotalKg : null,
                        direction == ParticipantFlightSegmentDirection.Return ? baggagePieces : null,
                        direction == ParticipantFlightSegmentDirection.Return ? baggageTotalKg : null,
                        "flight_segment",
                        direction.ToString(),
                        segmentIndex,
                        departureAirport,
                        arrivalAirport,
                        flightCode,
                        departureDate?.ToString("yyyy-MM-dd"),
                        departureTime?.ToString("HH:mm"),
                        arrivalDate?.ToString("yyyy-MM-dd"),
                        arrivalTime?.ToString("HH:mm"),
                        cabinBaggage));
                }
            }

            if (importMode == ParticipantImportMode.Apply && parsedFlightSegmentRows.Count > 0)
            {
                var participantIds = parsedFlightSegmentRows.Select(x => x.ParticipantId).Distinct().ToArray();
                var existingSegments = await db.ParticipantFlightSegments
                    .Where(x => x.OrganizationId == orgId && x.EventId == id && participantIds.Contains(x.ParticipantId))
                    .ToListAsync(ct);

                var segmentLookup = existingSegments.ToDictionary(
                    x => (x.ParticipantId, x.Direction, x.SegmentIndex),
                    x => x);

                foreach (var row in parsedFlightSegmentRows)
                {
                    var key = (row.ParticipantId, row.Direction, row.SegmentIndex);
                    if (!segmentLookup.TryGetValue(key, out var entity))
                    {
                        entity = new ParticipantFlightSegmentEntity
                        {
                            Id = Guid.NewGuid(),
                            OrganizationId = orgId,
                            EventId = id,
                            ParticipantId = row.ParticipantId,
                            Direction = row.Direction,
                            SegmentIndex = row.SegmentIndex
                        };
                        segmentLookup[key] = entity;
                        db.ParticipantFlightSegments.Add(entity);
                    }

                    entity.Airline = row.Airline;
                    entity.DepartureAirport = row.DepartureAirport;
                    entity.ArrivalAirport = row.ArrivalAirport;
                    entity.FlightCode = row.FlightCode;
                    entity.DepartureDate = row.DepartureDate;
                    entity.DepartureTime = row.DepartureTime;
                    entity.ArrivalDate = row.ArrivalDate;
                    entity.ArrivalTime = row.ArrivalTime;
                    entity.Pnr = row.Pnr;
                    entity.TicketNo = row.TicketNo;
                    entity.BaggagePieces = row.BaggagePieces;
                    entity.BaggageTotalKg = row.BaggageTotalKg;
                    entity.CabinBaggage = row.CabinBaggage;
                }
            }

            imported += parsedFlightSegmentRows.Count;

            var report = BuildReport(payload, imported, created, updated, errors, warnings, previewRows);

            if (importMode == ParticipantImportMode.DryRun)
            {
                return Results.Ok(report);
            }

            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
            return Results.Ok(report);
        }
        catch (Exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            return Results.Problem(
                title: "Participant import failed",
                detail: "No rows were written. Please retry with the same file.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static byte[] BuildCsvTemplate()
    {
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        foreach (var header in ParticipantTemplateHeaders)
        {
            csv.WriteField(header);
        }
        csv.NextRecord();

        foreach (var value in ParticipantTemplateExampleRow)
        {
            csv.WriteField(value);
        }
        csv.NextRecord();

        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    private static byte[] BuildXlsxTemplate()
    {
        using var workbook = new XLWorkbook();
        var participantSheet = workbook.AddWorksheet("participants");
        var flightSegmentSheet = workbook.AddWorksheet("flight_segments");

        var dateHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "birth_date",
            "hotel_check_in_date",
            "hotel_check_out_date",
            "insurance_start_date",
            "insurance_end_date",
            "arrival_flight_date",
            "return_flight_date",
            "departure_date",
            "arrival_date"
        };
        var timeHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "arrival_departure_time",
            "arrival_arrival_time",
            "return_departure_time",
            "return_arrival_time",
            "arrival_transfer_pickup_time",
            "return_transfer_pickup_time",
            "departure_time",
            "arrival_time"
        };

        for (var i = 0; i < ParticipantTemplateHeaders.Length; i++)
        {
            var header = ParticipantTemplateHeaders[i];
            var example = ParticipantTemplateExampleRow[i];
            var headerCell = participantSheet.Cell(1, i + 1);
            var valueCell = participantSheet.Cell(2, i + 1);

            headerCell.Value = header;

            if (dateHeaders.Contains(header)
                && DateTime.TryParseExact(example, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
            {
                valueCell.Value = dateValue;
                valueCell.Style.DateFormat.Format = "yyyy-MM-dd";
            }
            else if (timeHeaders.Contains(header)
                && TimeSpan.TryParseExact(example, "hh\\:mm", CultureInfo.InvariantCulture, out var timeValue))
            {
                valueCell.Value = timeValue;
                valueCell.Style.DateFormat.Format = "HH:mm";
            }
            else
            {
                valueCell.Value = example;
            }
        }

        participantSheet.Row(1).Style.Font.Bold = true;
        participantSheet.Columns().AdjustToContents();

        var flightSegmentTemplateRows = new[]
        {
            FlightSegmentExampleRows1,
            FlightSegmentExampleRows2,
            FlightSegmentExampleRows3,
            FlightSegmentExampleRows4
        };

        for (var i = 0; i < FlightSegmentCanonicalHeaders.Length; i++)
        {
            var header = FlightSegmentCanonicalHeaders[i];
            var headerCell = flightSegmentSheet.Cell(1, i + 1);

            headerCell.Value = header;
            for (var rowIndex = 0; rowIndex < flightSegmentTemplateRows.Length; rowIndex++)
            {
                var valueCell = flightSegmentSheet.Cell(rowIndex + 2, i + 1);
                var example = flightSegmentTemplateRows[rowIndex][i];
                WriteTemplateCell(valueCell, header, example, dateHeaders, timeHeaders);
            }
        }

        flightSegmentSheet.Row(1).Style.Font.Bold = true;
        flightSegmentSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteTemplateCell(
        IXLCell valueCell,
        string header,
        string example,
        HashSet<string> dateHeaders,
        HashSet<string> timeHeaders)
    {
        if (dateHeaders.Contains(header)
            && DateTime.TryParseExact(example, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
        {
            valueCell.Value = dateValue;
            valueCell.Style.DateFormat.Format = "yyyy-MM-dd";
            return;
        }

        if (timeHeaders.Contains(header)
            && TimeSpan.TryParseExact(example, "hh\\:mm", CultureInfo.InvariantCulture, out var timeValue))
        {
            valueCell.Value = timeValue;
            valueCell.Style.DateFormat.Format = "HH:mm";
            return;
        }

        valueCell.Value = example;
    }

    private static ImportPayload ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null
        });

        if (!csv.Read())
        {
            return new ImportPayload([], [], false, [], false, [], [], [], false);
        }

        csv.ReadHeader();
        var headerRecord = csv.Context.Reader.HeaderRecord ?? Array.Empty<string>();
        var headerMap = BuildHeaderMap(headerRecord);

        var rows = new List<ImportRow>();
        while (csv.Read())
        {
            var rowValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headerMap.CanonicalIndexes)
            {
                rowValues[pair.Key] = csv.GetField(pair.Value)?.Trim();
            }

            if (headerMap.FirstNameIndex is not null)
            {
                rowValues["first_name"] = csv.GetField(headerMap.FirstNameIndex.Value)?.Trim();
            }

            if (headerMap.LastNameIndex is not null)
            {
                rowValues["last_name"] = csv.GetField(headerMap.LastNameIndex.Value)?.Trim();
            }

            if (rowValues.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new ImportRow(rows.Count + 2, rowValues, headerMap.HasDetails));
        }

        return new ImportPayload(
            rows,
            headerMap.IgnoredColumns,
            headerMap.HasDetails,
            headerMap.MissingRequiredColumns,
            headerMap.LegacyHeadersDetected,
            [],
            [],
            [],
            false);
    }

    private static ImportPayload ReadXlsx(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var participantWorksheet = FindWorksheet(workbook, "participants") ?? workbook.Worksheets.FirstOrDefault();
        if (participantWorksheet is null)
        {
            return new ImportPayload([], [], false, [], false, [], [], [], false);
        }

        var participantSheetData = ReadParticipantSheet(participantWorksheet);
        var flightSegmentsWorksheet = FindWorksheet(workbook, "flight_segments");
        var flightSegmentsSheetData = ReadFlightSegmentSheet(flightSegmentsWorksheet);

        return new ImportPayload(
            participantSheetData.Rows,
            participantSheetData.HeaderMap.IgnoredColumns,
            participantSheetData.HeaderMap.HasDetails,
            participantSheetData.HeaderMap.MissingRequiredColumns,
            participantSheetData.HeaderMap.LegacyHeadersDetected,
            flightSegmentsSheetData.Rows,
            flightSegmentsSheetData.IgnoredColumns,
            flightSegmentsSheetData.MissingRequiredColumns,
            flightSegmentsSheetData.LegacyHeadersDetected);
    }

    private static ParticipantSheetData ReadParticipantSheet(IXLWorksheet worksheet)
    {
        var headers = ReadWorksheetHeaders(worksheet, out var lastColumn);
        if (lastColumn == 0)
        {
            return new ParticipantSheetData([], new HeaderMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                null, null, [], false, GetMissingRequiredColumns(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), null, null), false));
        }

        var headerMap = BuildHeaderMap(headers);
        var rows = new List<ImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var rowValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headerMap.CanonicalIndexes)
            {
                rowValues[pair.Key] = ReadCellValue(worksheet.Cell(row, pair.Value + 1), pair.Key);
            }

            if (headerMap.FirstNameIndex is not null)
            {
                rowValues["first_name"] = worksheet.Cell(row, headerMap.FirstNameIndex.Value + 1).GetString().Trim();
            }

            if (headerMap.LastNameIndex is not null)
            {
                rowValues["last_name"] = worksheet.Cell(row, headerMap.LastNameIndex.Value + 1).GetString().Trim();
            }

            if (rowValues.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new ImportRow(row, rowValues, headerMap.HasDetails));
        }

        return new ParticipantSheetData(rows, headerMap);
    }

    private static FlightSegmentSheetData ReadFlightSegmentSheet(IXLWorksheet? worksheet)
    {
        if (worksheet is null)
        {
            return new FlightSegmentSheetData([], [], [], false);
        }

        var headers = ReadWorksheetHeaders(worksheet, out var lastColumn);
        if (lastColumn == 0)
        {
            return new FlightSegmentSheetData([], [], [], false);
        }

        var headerMap = BuildFlightSegmentHeaderMap(headers);
        var rows = new List<FlightSegmentImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var rowValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headerMap.CanonicalIndexes)
            {
                rowValues[pair.Key] = ReadCellValue(worksheet.Cell(row, pair.Value + 1), pair.Key);
            }

            if (rowValues.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new FlightSegmentImportRow(row, rowValues));
        }

        return new FlightSegmentSheetData(
            rows,
            headerMap.IgnoredColumns,
            headerMap.MissingRequiredColumns,
            headerMap.LegacyHeadersDetected);
    }

    private static List<string> ReadWorksheetHeaders(IXLWorksheet worksheet, out int lastColumn)
    {
        lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var headers = new List<string>();
        for (var col = 1; col <= lastColumn; col++)
        {
            headers.Add(worksheet.Cell(1, col).GetString());
        }

        return headers;
    }

    private static IXLWorksheet? FindWorksheet(XLWorkbook workbook, string expectedName)
    {
        var normalizedExpected = NormalizeHeader(expectedName);
        return workbook.Worksheets.FirstOrDefault(x => NormalizeHeader(x.Name) == normalizedExpected);
    }

    private static string? ReadCellValue(IXLCell cell, string canonicalHeader)
    {
        if (DateColumns.Contains(canonicalHeader) && cell.TryGetValue<DateTime>(out var dateValue))
        {
            return DateOnly.FromDateTime(dateValue).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (TimeColumns.Contains(canonicalHeader))
        {
            if (cell.TryGetValue<TimeSpan>(out var timeValue))
            {
                return TimeOnly.FromTimeSpan(timeValue).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }

            if (cell.TryGetValue<DateTime>(out var dateTimeValue))
            {
                return TimeOnly.FromDateTime(dateTimeValue).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        return cell.GetString().Trim();
    }

    private static HeaderMap BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var aliasMap = BuildAliasMap();
        var canonicalIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ignored = new List<string>();
        int? firstNameIndex = null;
        int? lastNameIndex = null;
        var legacyHeadersDetected = false;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i] ?? string.Empty;
            var normalized = NormalizeHeader(header);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                ignored.Add(header);
                continue;
            }

            if (!aliasMap.TryGetValue(normalized, out var canonical))
            {
                ignored.Add(header);
                continue;
            }

            var canonicalNormalized = NormalizeHeader(canonical);
            if (!normalized.Equals(canonicalNormalized, StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("arrival_baggage_allowance", StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("return_baggage_allowance", StringComparison.OrdinalIgnoreCase))
            {
                legacyHeadersDetected = true;
            }

            if (canonical.Equals("full_name", StringComparison.OrdinalIgnoreCase))
            {
                legacyHeadersDetected = true;
            }

            if (canonical.Equals("first_name", StringComparison.OrdinalIgnoreCase))
            {
                firstNameIndex = i;
                continue;
            }

            if (canonical.Equals("last_name", StringComparison.OrdinalIgnoreCase))
            {
                lastNameIndex = i;
                continue;
            }

            if (!canonicalIndexes.ContainsKey(canonical))
            {
                canonicalIndexes[canonical] = i;
            }
        }

        var hasDetails = canonicalIndexes.Keys.Any(key =>
            key is not "first_name"
                and not "last_name"
                and not "full_name"
                and not "phone"
                and not "tc_no"
                and not "birth_date"
                and not "gender"
                and not "email");

        var missingRequiredColumns = GetMissingRequiredColumns(canonicalIndexes, firstNameIndex, lastNameIndex);

        return new HeaderMap(
            canonicalIndexes,
            firstNameIndex,
            lastNameIndex,
            ignored.ToArray(),
            hasDetails,
            missingRequiredColumns,
            legacyHeadersDetected);
    }

    private static FlightSegmentHeaderMap BuildFlightSegmentHeaderMap(IReadOnlyList<string> headers)
    {
        var aliasMap = BuildFlightSegmentAliasMap();
        var canonicalIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ignored = new List<string>();
        var legacyHeadersDetected = false;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i] ?? string.Empty;
            var normalized = NormalizeHeader(header);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                ignored.Add(header);
                continue;
            }

            if (!aliasMap.TryGetValue(normalized, out var canonical))
            {
                ignored.Add(header);
                continue;
            }

            var canonicalNormalized = NormalizeHeader(canonical);
            if (!normalized.Equals(canonicalNormalized, StringComparison.OrdinalIgnoreCase))
            {
                legacyHeadersDetected = true;
            }

            if (!canonicalIndexes.ContainsKey(canonical))
            {
                canonicalIndexes[canonical] = i;
            }
        }

        var missingRequiredColumns = FlightSegmentRequiredHeaders
            .Where(required => !canonicalIndexes.ContainsKey(required))
            .ToArray();

        return new FlightSegmentHeaderMap(
            canonicalIndexes,
            ignored.ToArray(),
            missingRequiredColumns,
            legacyHeadersDetected);
    }

    private static Dictionary<string, string> BuildAliasMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in CanonicalHeaders)
        {
            map[NormalizeHeader(header)] = header;
        }

        map[NormalizeHeader("adsoyad")] = "full_name";
        map[NormalizeHeader("ad soyad")] = "full_name";
        map[NormalizeHeader("ad + soyad")] = "full_name";
        map[NormalizeHeader("ad")] = "first_name";
        map[NormalizeHeader("soyad")] = "last_name";

        map[NormalizeHeader("oda no")] = "room_no";
        map[NormalizeHeader("oda no.")] = "room_no";
        map[NormalizeHeader("oda tipi")] = "room_type";
        map[NormalizeHeader("pansiyon")] = "board_type";
        map[NormalizeHeader("board type")] = "board_type";
        map[NormalizeHeader("kisi no")] = "person_no";
        map[NormalizeHeader("bayi adi")] = "agency_name";
        map[NormalizeHeader("acente adi")] = "agency_name";
        map[NormalizeHeader("sehir")] = "city";
        map[NormalizeHeader("ucus sehri")] = "flight_city";
        map[NormalizeHeader("dogum tarihi")] = "birth_date";
        map[NormalizeHeader("tc kimlik no")] = "tc_no";
        map[NormalizeHeader("tckimlikno")] = "tc_no";
        map[NormalizeHeader("telefon")] = "phone";
        map[NormalizeHeader("email")] = "email";
        map[NormalizeHeader("e mail")] = "email";
        map[NormalizeHeader("e-mail")] = "email";
        map[NormalizeHeader("checkin")] = "hotel_check_in_date";
        map[NormalizeHeader("checkout")] = "hotel_check_out_date";
        map[NormalizeHeader("bilet no")] = "arrival_ticket_no";
        map[NormalizeHeader("bilet no gidis")] = "arrival_ticket_no";
        map[NormalizeHeader("bilet no gidiş")] = "arrival_ticket_no";
        map[NormalizeHeader("gidis bilet no")] = "arrival_ticket_no";
        map[NormalizeHeader("gidiş bilet no")] = "arrival_ticket_no";
        map[NormalizeHeader("bilet no donus")] = "return_ticket_no";
        map[NormalizeHeader("bilet no dönüş")] = "return_ticket_no";
        map[NormalizeHeader("donus bilet no")] = "return_ticket_no";
        map[NormalizeHeader("dönüş bilet no")] = "return_ticket_no";
        map[NormalizeHeader("ticket_no")] = "arrival_ticket_no";
        map[NormalizeHeader("arrival ticket no")] = "arrival_ticket_no";
        map[NormalizeHeader("return ticket no")] = "return_ticket_no";
        map[NormalizeHeader("attendance_status")] = "attendance_status";
        map[NormalizeHeader("katilim durumu")] = "attendance_status";
        map[NormalizeHeader("sigorta sirketi")] = "insurance_company_name";
        map[NormalizeHeader("sigorta şirketi")] = "insurance_company_name";
        map[NormalizeHeader("sigorta polisi")] = "insurance_policy_no";
        map[NormalizeHeader("sigorta police no")] = "insurance_policy_no";
        map[NormalizeHeader("sigorta baslangic")] = "insurance_start_date";
        map[NormalizeHeader("sigorta başlangic")] = "insurance_start_date";
        map[NormalizeHeader("sigorta bitis")] = "insurance_end_date";
        map[NormalizeHeader("sigorta bitiş")] = "insurance_end_date";

        map[NormalizeHeader("gelis havayolu")] = "arrival_airline";
        map[NormalizeHeader("gelis kalkis havalimani")] = "arrival_departure_airport";
        map[NormalizeHeader("gelis varis havalimani")] = "arrival_arrival_airport";
        map[NormalizeHeader("gelis ucus kodu")] = "arrival_flight_code";
        map[NormalizeHeader("gelis kalkis saati")] = "arrival_departure_time";
        map[NormalizeHeader("gelis varis saati")] = "arrival_arrival_time";
        map[NormalizeHeader("gelis pnr")] = "arrival_pnr";
        map[NormalizeHeader("arrival_baggage_allowance")] = "arrival_baggage_allowance";
        map[NormalizeHeader("gelis bagajhakki")] = "arrival_baggage_allowance";
        map[NormalizeHeader("gidis bagajhakki")] = "arrival_baggage_allowance";

        map[NormalizeHeader("donus havayolu")] = "return_airline";
        map[NormalizeHeader("donus kalkis havalimani")] = "return_departure_airport";
        map[NormalizeHeader("donus varis havalimani")] = "return_arrival_airport";
        map[NormalizeHeader("donus ucus kodu")] = "return_flight_code";
        map[NormalizeHeader("donus kalkis saati")] = "return_departure_time";
        map[NormalizeHeader("donus varis saati")] = "return_arrival_time";
        map[NormalizeHeader("donus pnr")] = "return_pnr";
        map[NormalizeHeader("return_baggage_allowance")] = "return_baggage_allowance";
        map[NormalizeHeader("donus bagajhakki")] = "return_baggage_allowance";

        map[NormalizeHeader("gelis transfer alinis saati")] = "arrival_transfer_pickup_time";
        map[NormalizeHeader("gelis transfer alinis yeri")] = "arrival_transfer_pickup_place";
        map[NormalizeHeader("gelis transfer birakilis yeri")] = "arrival_transfer_dropoff_place";
        map[NormalizeHeader("gelis transfer bırakılış yeri")] = "arrival_transfer_dropoff_place";
        map[NormalizeHeader("gelis transfer arac")] = "arrival_transfer_vehicle";
        map[NormalizeHeader("gelis transfer araç")] = "arrival_transfer_vehicle";
        map[NormalizeHeader("gelis transfer plaka")] = "arrival_transfer_plate";
        map[NormalizeHeader("gelis transfer surucu bilgileri")] = "arrival_transfer_driver_info";
        map[NormalizeHeader("gelis transfer sürücü bilgileri")] = "arrival_transfer_driver_info";
        map[NormalizeHeader("gelis transfer not")] = "arrival_transfer_note";

        map[NormalizeHeader("donus transfer alinis saati")] = "return_transfer_pickup_time";
        map[NormalizeHeader("donus transfer alinis yeri")] = "return_transfer_pickup_place";
        map[NormalizeHeader("donus transfer birakilis yeri")] = "return_transfer_dropoff_place";
        map[NormalizeHeader("donus transfer bırakılış yeri")] = "return_transfer_dropoff_place";
        map[NormalizeHeader("donus transfer arac")] = "return_transfer_vehicle";
        map[NormalizeHeader("donus transfer araç")] = "return_transfer_vehicle";
        map[NormalizeHeader("donus transfer plaka")] = "return_transfer_plate";
        map[NormalizeHeader("donus transfer surucu bilgileri")] = "return_transfer_driver_info";
        map[NormalizeHeader("donus transfer sürücü bilgileri")] = "return_transfer_driver_info";
        map[NormalizeHeader("donus transfer not")] = "return_transfer_note";

        return map;
    }

    private static Dictionary<string, string> BuildFlightSegmentAliasMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in FlightSegmentCanonicalHeaders)
        {
            map[NormalizeHeader(header)] = header;
        }

        map[NormalizeHeader("tc kimlik no")] = "tc_no";
        map[NormalizeHeader("full_name")] = "participant_name";
        map[NormalizeHeader("ad soyad")] = "participant_name";
        map[NormalizeHeader("adsoyad")] = "participant_name";
        map[NormalizeHeader("yon")] = "direction";
        map[NormalizeHeader("yön")] = "direction";
        map[NormalizeHeader("segment no")] = "segment_index";
        map[NormalizeHeader("baglanti no")] = "segment_index";
        map[NormalizeHeader("bağlantı no")] = "segment_index";
        map[NormalizeHeader("kalkis havalimani")] = "departure_airport";
        map[NormalizeHeader("kalkış havalimanı")] = "departure_airport";
        map[NormalizeHeader("varis havalimani")] = "arrival_airport";
        map[NormalizeHeader("varış havalimanı")] = "arrival_airport";
        map[NormalizeHeader("ucus kodu")] = "flight_code";
        map[NormalizeHeader("uçuş kodu")] = "flight_code";
        map[NormalizeHeader("kalkis tarihi")] = "departure_date";
        map[NormalizeHeader("kalkış tarihi")] = "departure_date";
        map[NormalizeHeader("kalkis saati")] = "departure_time";
        map[NormalizeHeader("kalkış saati")] = "departure_time";
        map[NormalizeHeader("varis tarihi")] = "arrival_date";
        map[NormalizeHeader("varış tarihi")] = "arrival_date";
        map[NormalizeHeader("varis saati")] = "arrival_time";
        map[NormalizeHeader("varış saati")] = "arrival_time";
        map[NormalizeHeader("bagaj parcasi")] = "baggage_pieces";
        map[NormalizeHeader("bagaj parçası")] = "baggage_pieces";
        map[NormalizeHeader("bagaj toplam kg")] = "baggage_total_kg";
        map[NormalizeHeader("cabin baggage")] = "cabin_baggage";
        map[NormalizeHeader("kabin bagaji")] = "cabin_baggage";
        map[NormalizeHeader("kabin bagajı")] = "cabin_baggage";

        return map;
    }

    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }
        return sb.ToString();
    }

    private static bool NamesMatchForReference(string? source, string? target)
        => NormalizeReferenceName(source) == NormalizeReferenceName(target);

    private static string NormalizeReferenceName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var lastWasWhitespace = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && !lastWasWhitespace)
                {
                    sb.Append(' ');
                }
                lastWasWhitespace = true;
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
            lastWasWhitespace = false;
        }

        return sb.ToString().Trim();
    }

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return hasPlus ? $"+{digits}" : digits;
    }

    private static string NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    private static string BuildFullName(string? first, string? last)
    {
        var firstName = NormalizeName(first);
        var lastName = NormalizeName(last);
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return lastName;
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return firstName;
        }

        return $"{firstName} {lastName}";
    }

    private static void SplitFullName(string? fullName, out string firstName, out string lastName)
    {
        var normalized = NormalizeName(fullName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            firstName = string.Empty;
            lastName = string.Empty;
            return;
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            firstName = parts[0];
            lastName = parts[0];
            return;
        }

        lastName = parts[^1];
        firstName = string.Join(' ', parts[..^1]);
    }

    private static bool TryParseDate(string? value, out DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        return DateOnly.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseOptionalDate(string? value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }

        if (TryParseDate(value, out var parsed))
        {
            date = parsed;
            return true;
        }

        date = null;
        return false;
    }

    private static bool TryParseOptionalTime(string? value, out TimeOnly? time)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            time = null;
            return true;
        }

        if (TimeOnly.TryParseExact(value.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = parsed;
            return true;
        }

        time = null;
        return false;
    }

    private static bool TryParseSegmentIndex(string? value, out int segmentIndex)
    {
        if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out segmentIndex))
        {
            segmentIndex = 0;
            return false;
        }

        return segmentIndex >= 1;
    }

    private static bool TryParseFlightSegmentDirection(string? value, out ParticipantFlightSegmentDirection direction)
    {
        var normalized = NormalizeHeader(value ?? string.Empty);
        switch (normalized)
        {
            case "arrival":
            case "gidis":
                direction = ParticipantFlightSegmentDirection.Arrival;
                return true;
            case "return":
            case "donus":
                direction = ParticipantFlightSegmentDirection.Return;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static bool TryParseOptionalBaggagePieces(string? value, out int? pieces, out bool decimalProvided)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            pieces = null;
            decimalProvided = false;
            return true;
        }

        var trimmed = value.Trim();
        var pieceMatch = BaggagePiecesRegex.Match(trimmed);
        var numericValue = pieceMatch.Success ? pieceMatch.Groups["value"].Value : trimmed;

        return TryParseBaggagePiecesValue(numericValue, out pieces, out decimalProvided);
    }

    private static bool TryParseOptionalBaggageTotalKg(string? value, out int? totalKg, out bool rounded)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            totalKg = null;
            rounded = false;
            return true;
        }

        var trimmed = value.Trim();
        var kgMatch = BaggageKgRegex.Match(trimmed);
        var numericValue = kgMatch.Success ? kgMatch.Groups["value"].Value : trimmed;

        return TryParseBaggageKgValue(numericValue, out totalKg, out rounded);
    }

    private static BaggageAllowanceParseResult TryParseBaggageAllowance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BaggageAllowanceParseResult.Empty;
        }

        int? pieces = null;
        int? totalKg = null;
        var piecesDecimal = false;
        var piecesInvalid = false;
        var kgRounded = false;
        var kgInvalid = false;

        var trimmed = value.Trim();
        var pieceMatch = BaggagePiecesRegex.Match(trimmed);
        if (pieceMatch.Success)
        {
            if (!TryParseBaggagePiecesValue(pieceMatch.Groups["value"].Value, out pieces, out piecesDecimal))
            {
                piecesInvalid = !piecesDecimal;
            }
        }

        var kgMatch = BaggageKgRegex.Match(trimmed);
        if (kgMatch.Success)
        {
            if (!TryParseBaggageKgValue(kgMatch.Groups["value"].Value, out totalKg, out kgRounded))
            {
                kgInvalid = true;
            }
        }

        return new BaggageAllowanceParseResult(pieces, totalKg, piecesDecimal, piecesInvalid, kgRounded, kgInvalid);
    }

    private static bool TryParseBaggagePiecesValue(string value, out int? pieces, out bool decimalProvided)
    {
        if (!TryParseNumeric(value, out var number))
        {
            pieces = null;
            decimalProvided = false;
            return false;
        }

        if (!IsWholeNumber(number))
        {
            pieces = null;
            decimalProvided = true;
            return false;
        }

        var intValue = (int)Math.Round(number, MidpointRounding.AwayFromZero);
        if (intValue <= 0)
        {
            pieces = null;
            decimalProvided = false;
            return false;
        }

        pieces = intValue;
        decimalProvided = false;
        return true;
    }

    private static bool TryParseBaggageKgValue(string value, out int? totalKg, out bool rounded)
    {
        if (!TryParseNumeric(value, out var number))
        {
            totalKg = null;
            rounded = false;
            return false;
        }

        if (number <= 0)
        {
            totalKg = null;
            rounded = false;
            return false;
        }

        if (IsWholeNumber(number))
        {
            totalKg = (int)Math.Round(number, MidpointRounding.AwayFromZero);
            rounded = false;
            return true;
        }

        rounded = true;
        totalKg = RoundToInt(number);
        if (totalKg <= 0)
        {
            totalKg = null;
            return false;
        }

        return true;
    }

    private static bool TryParseNumeric(string value, out double number)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        if (value.Contains(','))
        {
            var normalized = value.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return true;
            }
        }

        number = 0;
        return false;
    }

    private static int RoundToInt(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static bool IsWholeNumber(double value)
        => Math.Abs(value % 1) < 0.0001;

    private sealed record BaggageAllowanceParseResult(
        int? Pieces,
        int? TotalKg,
        bool PiecesDecimal,
        bool PiecesInvalid,
        bool KgRounded,
        bool KgInvalid)
    {
        public static readonly BaggageAllowanceParseResult Empty = new(null, null, false, false, false, false);
    }

    private static bool TryParseGender(string? value, out ParticipantGender gender)
    {
        var normalized = NormalizeHeader(value ?? string.Empty);
        switch (normalized)
        {
            case "female":
            case "f":
            case "kadin":
            case "kadinlar":
                gender = ParticipantGender.Female;
                return true;
            case "male":
            case "m":
            case "erkek":
                gender = ParticipantGender.Male;
                return true;
            case "other":
            case "o":
            case "diger":
            case "digeri":
                gender = ParticipantGender.Other;
                return true;
            default:
                gender = ParticipantGender.Other;
                return false;
        }
    }

    private static string NormalizeTcNo(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string GenerateUniqueCheckInCode(HashSet<string> usedCodes)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[8];
        Span<char> chars = stackalloc char[8];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            RandomNumberGenerator.Fill(bytes);
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }
            var code = new string(chars);
            if (usedCodes.Add(code))
            {
                return code;
            }
        }

        var fallback = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        usedCodes.Add(fallback);
        return fallback;
    }

    private static void ApplyDetails(
        ParticipantDetailsEntity details,
        ImportRow row,
        DateOnly? hotelCheckIn,
        DateOnly? hotelCheckOut,
        DateOnly? insuranceStart,
        DateOnly? insuranceEnd,
        DateOnly? arrivalFlightDate,
        DateOnly? returnFlightDate,
        TimeOnly? arrivalDeparture,
        TimeOnly? arrivalArrival,
        TimeOnly? returnDeparture,
        TimeOnly? returnArrival,
        int? arrivalBaggagePieces,
        int? arrivalBaggageTotalKg,
        int? returnBaggagePieces,
        int? returnBaggageTotalKg,
        TimeOnly? arrivalTransferPickupTime,
        TimeOnly? returnTransferPickupTime,
        bool includeLegacyFlightFields)
    {
        details.RoomNo = row.GetValue("room_no");
        details.RoomType = row.GetValue("room_type");
        details.BoardType = row.GetValue("board_type");
        details.PersonNo = row.GetValue("person_no");
        details.AgencyName = row.GetValue("agency_name");
        details.City = row.GetValue("city");
        details.FlightCity = row.GetValue("flight_city");
        details.HotelCheckInDate = hotelCheckIn;
        details.HotelCheckOutDate = hotelCheckOut;
        details.AttendanceStatus = row.GetValue("attendance_status");
        details.InsuranceCompanyName = row.GetValue("insurance_company_name");
        details.InsurancePolicyNo = row.GetValue("insurance_policy_no");
        details.InsuranceStartDate = insuranceStart;
        details.InsuranceEndDate = insuranceEnd;

        if (includeLegacyFlightFields)
        {
            var arrivalTicketNo = row.GetValue("arrival_ticket_no");
            var returnTicketNo = row.GetValue("return_ticket_no");
            details.ArrivalTicketNo = arrivalTicketNo;
            details.ReturnTicketNo = returnTicketNo;
            if (string.IsNullOrWhiteSpace(details.TicketNo) && !string.IsNullOrWhiteSpace(arrivalTicketNo))
            {
                details.TicketNo = arrivalTicketNo;
            }

            details.ArrivalAirline = row.GetValue("arrival_airline");
            details.ArrivalDepartureAirport = row.GetValue("arrival_departure_airport");
            details.ArrivalArrivalAirport = row.GetValue("arrival_arrival_airport");
            details.ArrivalFlightCode = row.GetValue("arrival_flight_code");
            details.ArrivalFlightDate = arrivalFlightDate;
            details.ArrivalDepartureTime = arrivalDeparture;
            details.ArrivalArrivalTime = arrivalArrival;
            details.ArrivalPnr = row.GetValue("arrival_pnr");
            details.ArrivalBaggageAllowance = row.GetValue("arrival_baggage_allowance");
            details.ArrivalBaggagePieces = arrivalBaggagePieces;
            details.ArrivalBaggageTotalKg = arrivalBaggageTotalKg;
            details.ArrivalCabinBaggage = row.GetValue("arrival_cabin_baggage");
            details.ReturnAirline = row.GetValue("return_airline");
            details.ReturnDepartureAirport = row.GetValue("return_departure_airport");
            details.ReturnArrivalAirport = row.GetValue("return_arrival_airport");
            details.ReturnFlightCode = row.GetValue("return_flight_code");
            details.ReturnFlightDate = returnFlightDate;
            details.ReturnDepartureTime = returnDeparture;
            details.ReturnArrivalTime = returnArrival;
            details.ReturnPnr = row.GetValue("return_pnr");
            details.ReturnBaggageAllowance = row.GetValue("return_baggage_allowance");
            details.ReturnBaggagePieces = returnBaggagePieces;
            details.ReturnBaggageTotalKg = returnBaggageTotalKg;
            details.ReturnCabinBaggage = row.GetValue("return_cabin_baggage");
        }

        details.ArrivalTransferPickupTime = arrivalTransferPickupTime;
        details.ArrivalTransferPickupPlace = row.GetValue("arrival_transfer_pickup_place");
        details.ArrivalTransferDropoffPlace = row.GetValue("arrival_transfer_dropoff_place");
        details.ArrivalTransferVehicle = row.GetValue("arrival_transfer_vehicle");
        details.ArrivalTransferPlate = row.GetValue("arrival_transfer_plate");
        details.ArrivalTransferDriverInfo = row.GetValue("arrival_transfer_driver_info");
        details.ArrivalTransferNote = row.GetValue("arrival_transfer_note");
        details.ReturnTransferPickupTime = returnTransferPickupTime;
        details.ReturnTransferPickupPlace = row.GetValue("return_transfer_pickup_place");
        details.ReturnTransferDropoffPlace = row.GetValue("return_transfer_dropoff_place");
        details.ReturnTransferVehicle = row.GetValue("return_transfer_vehicle");
        details.ReturnTransferPlate = row.GetValue("return_transfer_plate");
        details.ReturnTransferDriverInfo = row.GetValue("return_transfer_driver_info");
        details.ReturnTransferNote = row.GetValue("return_transfer_note");
    }

    private static bool HasAnyLegacyParticipantFlightValue(List<ImportRow> rows)
    {
        foreach (var row in rows)
        {
            foreach (var header in LegacyParticipantFlightHeaders)
            {
                if (!string.IsNullOrWhiteSpace(row.GetValue(header)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Dictionary<string, List<int>> BuildFileTcRowMap(List<ImportRow> rows)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var tc = NormalizeTcNo(row.GetValue("tc_no"));
            if (string.IsNullOrWhiteSpace(tc))
            {
                continue;
            }

            if (!map.TryGetValue(tc, out var rowNumbers))
            {
                rowNumbers = [];
                map[tc] = rowNumbers;
            }

            rowNumbers.Add(row.RowNumber);
        }

        return map;
    }

    private static string[] GetMissingRequiredColumns(
        Dictionary<string, int> canonicalIndexes,
        int? firstNameIndex,
        int? lastNameIndex)
    {
        var missing = new List<string>();

        var hasFirstName = firstNameIndex is not null;
        var hasLastName = lastNameIndex is not null;
        var hasLegacyFullName = canonicalIndexes.ContainsKey("full_name");

        if (!hasFirstName && !hasLegacyFullName)
        {
            missing.Add("first_name");
        }

        if (!hasLastName && !hasLegacyFullName)
        {
            missing.Add("last_name");
        }

        if (!canonicalIndexes.ContainsKey("phone"))
        {
            missing.Add("phone");
        }

        if (!canonicalIndexes.ContainsKey("tc_no"))
        {
            missing.Add("tc_no");
        }

        if (!canonicalIndexes.ContainsKey("birth_date"))
        {
            missing.Add("birth_date");
        }

        if (!canonicalIndexes.ContainsKey("gender"))
        {
            missing.Add("gender");
        }

        return missing.ToArray();
    }

    private static ParticipantImportReport BuildReport(
        ImportPayload payload,
        int imported,
        int created,
        int updated,
        List<ParticipantImportError> errors,
        List<ParticipantImportWarning> warnings,
        List<ParticipantImportPreviewRow> previewRows)
    {
        var totalRows = payload.Rows.Count + payload.FlightSegmentRows.Count;
        var validRows = Math.Max(totalRows - errors.Count, 0);
        var skipped = Math.Max(totalRows - imported, 0);
        var previewTruncated = totalRows > PreviewRowLimit;
        var ignoredColumns = payload.IgnoredColumns
            .Concat(payload.FlightSegmentIgnoredColumns.Select(x => $"flight_segments.{x}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ParticipantImportReport(
            totalRows,
            validRows,
            imported,
            created,
            updated,
            skipped,
            errors.Count,
            errors.Count,
            PreviewRowLimit,
            previewTruncated,
            ignoredColumns,
            errors.OrderBy(x => x.Row).ToArray(),
            warnings.OrderBy(x => x.Row).ToArray(),
            previewRows.OrderBy(x => x.RowIndex).ToArray());
    }

    private sealed record ImportPayload(
        List<ImportRow> Rows,
        string[] IgnoredColumns,
        bool HasDetails,
        string[] MissingRequiredColumns,
        bool LegacyHeadersDetected,
        List<FlightSegmentImportRow> FlightSegmentRows,
        string[] FlightSegmentIgnoredColumns,
        string[] MissingFlightSegmentRequiredColumns,
        bool FlightSegmentLegacyHeadersDetected);
    private sealed record ImportRow(int RowNumber, Dictionary<string, string?> Values, bool HasAnyDetails)
    {
        public string? GetValue(string key)
            => Values.TryGetValue(key, out var value) ? value : null;
    }

    private sealed record FlightSegmentImportRow(int RowNumber, Dictionary<string, string?> Values)
    {
        public string? GetValue(string key)
            => Values.TryGetValue(key, out var value) ? value : null;
    }

    private sealed record ParsedFlightSegmentRow(
        int RowNumber,
        Guid ParticipantId,
        ParticipantFlightSegmentDirection Direction,
        int SegmentIndex,
        string? Airline,
        string? DepartureAirport,
        string? ArrivalAirport,
        string? FlightCode,
        DateOnly? DepartureDate,
        TimeOnly? DepartureTime,
        DateOnly? ArrivalDate,
        TimeOnly? ArrivalTime,
        string? Pnr,
        string? TicketNo,
        int? BaggagePieces,
        int? BaggageTotalKg,
        string? CabinBaggage,
        string? ParticipantNameReference,
        string TcNo);

    private sealed record ParticipantSheetData(List<ImportRow> Rows, HeaderMap HeaderMap);
    private sealed record FlightSegmentSheetData(
        List<FlightSegmentImportRow> Rows,
        string[] IgnoredColumns,
        string[] MissingRequiredColumns,
        bool LegacyHeadersDetected);

    private sealed record HeaderMap(
        Dictionary<string, int> CanonicalIndexes,
        int? FirstNameIndex,
        int? LastNameIndex,
        string[] IgnoredColumns,
        bool HasDetails,
        string[] MissingRequiredColumns,
        bool LegacyHeadersDetected);

    private sealed record FlightSegmentHeaderMap(
        Dictionary<string, int> CanonicalIndexes,
        string[] IgnoredColumns,
        string[] MissingRequiredColumns,
        bool LegacyHeadersDetected);
}
