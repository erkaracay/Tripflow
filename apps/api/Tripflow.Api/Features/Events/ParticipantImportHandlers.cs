using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly string[] CanonicalHeaders =
    [
        "room_no",
        "room_type",
        "person_no",
        "agency_name",
        "city",
        "full_name",
        "birth_date",
        "tc_no",
        "gender",
        "phone",
        "email",
        "flight_city",
        "hotel_check_in_date",
        "hotel_check_out_date",
        "ticket_no",
        "arrival_airline",
        "arrival_departure_airport",
        "arrival_arrival_airport",
        "arrival_flight_code",
        "arrival_departure_time",
        "arrival_arrival_time",
        "arrival_pnr",
        "arrival_baggage_allowance",
        "return_airline",
        "return_departure_airport",
        "return_arrival_airport",
        "return_flight_code",
        "return_departure_time",
        "return_arrival_time",
        "return_pnr",
        "return_baggage_allowance"
    ];

    private static readonly string[] ExampleRow =
    [
        "101",
        "Double",
        "2",
        "Sky Travel",
        "Istanbul",
        "Ayse Demir",
        "1990-05-10",
        "12345678901",
        "Female",
        "+905301112233",
        "ayse@example.com",
        "Istanbul",
        "2026-03-10",
        "2026-03-12",
        "TK-123",
        "Turkish Airlines",
        "IST",
        "ASR",
        "TK202",
        "08:15",
        "09:35",
        "PNR123",
        "1PC",
        "Turkish Airlines",
        "ASR",
        "IST",
        "TK303",
        "18:45",
        "20:10",
        "PNR456",
        "1PC"
    ];

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
            payload = extension == ".csv"
                ? ReadCsv(stream)
                : ReadXlsx(stream);
        }

        if (payload.Rows.Count > MaxRows)
        {
            return EventsHelpers.BadRequest($"Max {MaxRows} rows allowed.");
        }

        var errors = new List<ParticipantImportError>();
        var warnings = new List<ParticipantImportWarning>();

        if (payload.MissingRequiredColumns.Length > 0)
        {
            errors.Add(new ParticipantImportError(
                1,
                null,
                $"Missing required columns: {string.Join(", ", payload.MissingRequiredColumns)}",
                payload.MissingRequiredColumns));

            var missingColumnsReport = BuildReport(payload, 0, 0, 0, errors, warnings);
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
                    ["tc_no"]));
            }
        }

        var hasFatalFileErrors = duplicateTcInFile.Count > 0;
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
        await using var transaction = importMode == ParticipantImportMode.Apply && !hasFatalFileErrors
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

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

                var fullName = NormalizeName(row.GetValue("full_name"));
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = NormalizeName(JoinFirstLast(row.GetValue("first_name"), row.GetValue("last_name")));
                }

                var phone = NormalizePhone(row.GetValue("phone"));
                var email = NormalizeEmail(row.GetValue("email"));
                var genderRaw = row.GetValue("gender");

                var rowErrors = new List<string>();
                var errorFields = new List<string>();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    rowErrors.Add("full_name required");
                    errorFields.Add("full_name");
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

                if (!TryParseDate(row.GetValue("birth_date"), out var birthDate))
                {
                    rowErrors.Add("birth_date invalid");
                    errorFields.Add("birth_date");
                }

                if (!TryParseGender(genderRaw, out var gender))
                {
                    rowErrors.Add("gender invalid");
                    errorFields.Add("gender");
                }

                if (!TryParseOptionalDate(row.GetValue("hotel_check_in_date"), out var hotelCheckIn))
                {
                    rowErrors.Add("hotel_check_in_date invalid");
                    errorFields.Add("hotel_check_in_date");
                }

                if (!TryParseOptionalDate(row.GetValue("hotel_check_out_date"), out var hotelCheckOut))
                {
                    rowErrors.Add("hotel_check_out_date invalid");
                    errorFields.Add("hotel_check_out_date");
                }

                if (!TryParseOptionalTime(row.GetValue("arrival_departure_time"), out var arrivalDepartureTime))
                {
                    rowErrors.Add("arrival_departure_time invalid");
                    errorFields.Add("arrival_departure_time");
                }

                if (!TryParseOptionalTime(row.GetValue("arrival_arrival_time"), out var arrivalArrivalTime))
                {
                    rowErrors.Add("arrival_arrival_time invalid");
                    errorFields.Add("arrival_arrival_time");
                }

                if (!TryParseOptionalTime(row.GetValue("return_departure_time"), out var returnDepartureTime))
                {
                    rowErrors.Add("return_departure_time invalid");
                    errorFields.Add("return_departure_time");
                }

                if (!TryParseOptionalTime(row.GetValue("return_arrival_time"), out var returnArrivalTime))
                {
                    rowErrors.Add("return_arrival_time invalid");
                    errorFields.Add("return_arrival_time");
                }

                if (rowErrors.Count > 0)
                {
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        string.IsNullOrWhiteSpace(tcNo) ? null : tcNo,
                        string.Join("; ", rowErrors),
                        errorFields.ToArray()));
                    continue;
                }

                if (ambiguousDbTc.Contains(tcNo))
                {
                    errors.Add(new ParticipantImportError(
                        row.RowNumber,
                        tcNo,
                        "tc_no matches multiple existing participants in this event.",
                        ["tc_no"]));
                    continue;
                }

                var hasDetails = row.HasAnyDetails || hotelCheckIn is not null || hotelCheckOut is not null
                    || arrivalDepartureTime is not null || arrivalArrivalTime is not null
                    || returnDepartureTime is not null || returnArrivalTime is not null;

                if (existingByTc.TryGetValue(tcNo, out var matches) && matches.Count == 1)
                {
                    updated++;
                    warnings.Add(new ParticipantImportWarning(
                        row.RowNumber,
                        tcNo,
                        "tc_no already exists in event; row will update existing participant.",
                        "duplicate_tcno"));

                    if (importMode == ParticipantImportMode.Apply)
                    {
                        var participant = matches[0];
                        participant.FullName = fullName;
                        participant.Phone = phone;
                        participant.Email = email;
                        participant.TcNo = tcNo;
                        participant.BirthDate = birthDate;
                        participant.Gender = gender;

                        if (hasDetails)
                        {
                            participant.Details ??= new ParticipantDetailsEntity { ParticipantId = participant.Id };
                            ApplyDetails(participant.Details, row, hotelCheckIn, hotelCheckOut, arrivalDepartureTime,
                                arrivalArrivalTime, returnDepartureTime, returnArrivalTime);
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
                            FullName = fullName,
                            Phone = phone,
                            Email = email,
                            TcNo = tcNo,
                            BirthDate = birthDate,
                            Gender = gender,
                            CheckInCode = GenerateUniqueCheckInCode(usedCodes),
                            PortalFailedAttempts = 0,
                            CreatedAt = now
                        };

                        if (hasDetails)
                        {
                            var details = new ParticipantDetailsEntity
                            {
                                ParticipantId = newParticipant.Id
                            };
                            ApplyDetails(details, row, hotelCheckIn, hotelCheckOut, arrivalDepartureTime,
                                arrivalArrivalTime, returnDepartureTime, returnArrivalTime);
                            newParticipant.Details = details;
                        }

                        createdParticipants.Add(newParticipant);
                        existingByTc[tcNo] = [newParticipant];
                    }
                    else
                    {
                        existingByTc[tcNo] = [new ParticipantEntity { Id = Guid.NewGuid(), TcNo = tcNo }];
                    }
                }

                imported++;
            }

            var report = BuildReport(payload, imported, created, updated, errors, warnings);

            if (importMode == ParticipantImportMode.DryRun)
            {
                return Results.Ok(report);
            }

            if (hasFatalFileErrors)
            {
                return Results.BadRequest(report);
            }

            if (createdParticipants.Count > 0)
            {
                db.Participants.AddRange(createdParticipants);
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

        foreach (var header in CanonicalHeaders)
        {
            csv.WriteField(header);
        }
        csv.NextRecord();

        foreach (var value in ExampleRow)
        {
            csv.WriteField(value);
        }
        csv.NextRecord();

        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    private static byte[] BuildXlsxTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Participants");

        for (var i = 0; i < CanonicalHeaders.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = CanonicalHeaders[i];
            worksheet.Cell(2, i + 1).Value = ExampleRow[i];
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
            return new ImportPayload([], [], false, []);
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

        return new ImportPayload(rows, headerMap.IgnoredColumns, headerMap.HasDetails, headerMap.MissingRequiredColumns);
    }

    private static ImportPayload ReadXlsx(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastColumn == 0)
        {
            return new ImportPayload([], [], false, []);
        }

        var headers = new List<string>();
        for (var col = 1; col <= lastColumn; col++)
        {
            headers.Add(worksheet.Cell(1, col).GetString());
        }

        var headerMap = BuildHeaderMap(headers);

        var rows = new List<ImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var rowValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headerMap.CanonicalIndexes)
            {
                rowValues[pair.Key] = worksheet.Cell(row, pair.Value + 1).GetString().Trim();
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

        return new ImportPayload(rows, headerMap.IgnoredColumns, headerMap.HasDetails, headerMap.MissingRequiredColumns);
    }

    private static HeaderMap BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var aliasMap = BuildAliasMap();
        var canonicalIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ignored = new List<string>();
        int? firstNameIndex = null;
        int? lastNameIndex = null;

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

        var hasDetails = canonicalIndexes.Keys.Any(key => key is not "full_name" and not "phone" and not "tc_no" and not "birth_date" and not "gender" and not "email");

        var missingRequiredColumns = GetMissingRequiredColumns(canonicalIndexes, firstNameIndex, lastNameIndex);

        return new HeaderMap(
            canonicalIndexes,
            firstNameIndex,
            lastNameIndex,
            ignored.ToArray(),
            hasDetails,
            missingRequiredColumns);
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
        map[NormalizeHeader("bilet no")] = "ticket_no";
        map[NormalizeHeader("attendance_status")] = "attendance_status";
        map[NormalizeHeader("katilim durumu")] = "attendance_status";

        map[NormalizeHeader("gelis havayolu")] = "arrival_airline";
        map[NormalizeHeader("gelis kalkis havalimani")] = "arrival_departure_airport";
        map[NormalizeHeader("gelis varis havalimani")] = "arrival_arrival_airport";
        map[NormalizeHeader("gelis ucus kodu")] = "arrival_flight_code";
        map[NormalizeHeader("gelis kalkis saati")] = "arrival_departure_time";
        map[NormalizeHeader("gelis varis saati")] = "arrival_arrival_time";
        map[NormalizeHeader("gelis pnr")] = "arrival_pnr";
        map[NormalizeHeader("gelis bagajhakki")] = "arrival_baggage_allowance";
        map[NormalizeHeader("gidis bagajhakki")] = "arrival_baggage_allowance";

        map[NormalizeHeader("donus havayolu")] = "return_airline";
        map[NormalizeHeader("donus kalkis havalimani")] = "return_departure_airport";
        map[NormalizeHeader("donus varis havalimani")] = "return_arrival_airport";
        map[NormalizeHeader("donus ucus kodu")] = "return_flight_code";
        map[NormalizeHeader("donus kalkis saati")] = "return_departure_time";
        map[NormalizeHeader("donus varis saati")] = "return_arrival_time";
        map[NormalizeHeader("donus pnr")] = "return_pnr";
        map[NormalizeHeader("donus bagajhakki")] = "return_baggage_allowance";

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

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    private static string JoinFirstLast(string? first, string? last)
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

    private static bool TryParseDate(string? value, out DateOnly date)
    {
        var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy" };
        return DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
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

        if (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            time = parsed;
            return true;
        }

        time = null;
        return false;
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
        TimeOnly? arrivalDeparture,
        TimeOnly? arrivalArrival,
        TimeOnly? returnDeparture,
        TimeOnly? returnArrival)
    {
        details.RoomNo = row.GetValue("room_no");
        details.RoomType = row.GetValue("room_type");
        details.PersonNo = row.GetValue("person_no");
        details.AgencyName = row.GetValue("agency_name");
        details.City = row.GetValue("city");
        details.FlightCity = row.GetValue("flight_city");
        details.HotelCheckInDate = hotelCheckIn;
        details.HotelCheckOutDate = hotelCheckOut;
        details.TicketNo = row.GetValue("ticket_no");
        details.AttendanceStatus = row.GetValue("attendance_status");
        details.ArrivalAirline = row.GetValue("arrival_airline");
        details.ArrivalDepartureAirport = row.GetValue("arrival_departure_airport");
        details.ArrivalArrivalAirport = row.GetValue("arrival_arrival_airport");
        details.ArrivalFlightCode = row.GetValue("arrival_flight_code");
        details.ArrivalDepartureTime = arrivalDeparture;
        details.ArrivalArrivalTime = arrivalArrival;
        details.ArrivalPnr = row.GetValue("arrival_pnr");
        details.ArrivalBaggageAllowance = row.GetValue("arrival_baggage_allowance");
        details.ReturnAirline = row.GetValue("return_airline");
        details.ReturnDepartureAirport = row.GetValue("return_departure_airport");
        details.ReturnArrivalAirport = row.GetValue("return_arrival_airport");
        details.ReturnFlightCode = row.GetValue("return_flight_code");
        details.ReturnDepartureTime = returnDeparture;
        details.ReturnArrivalTime = returnArrival;
        details.ReturnPnr = row.GetValue("return_pnr");
        details.ReturnBaggageAllowance = row.GetValue("return_baggage_allowance");
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

        var hasFullName = canonicalIndexes.ContainsKey("full_name")
                          || (firstNameIndex is not null && lastNameIndex is not null);

        if (!hasFullName)
        {
            missing.Add("full_name");
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
        List<ParticipantImportWarning> warnings)
    {
        var totalRows = payload.Rows.Count;
        var validRows = Math.Max(totalRows - errors.Count, 0);
        var skipped = Math.Max(totalRows - imported, 0);

        return new ParticipantImportReport(
            totalRows,
            validRows,
            imported,
            created,
            updated,
            skipped,
            errors.Count,
            errors.Count,
            payload.IgnoredColumns,
            errors.OrderBy(x => x.Row).ToArray(),
            warnings.OrderBy(x => x.Row).ToArray());
    }

    private sealed record ImportPayload(List<ImportRow> Rows, string[] IgnoredColumns, bool HasDetails, string[] MissingRequiredColumns);
    private sealed record ImportRow(int RowNumber, Dictionary<string, string?> Values, bool HasAnyDetails)
    {
        public string? GetValue(string key)
            => Values.TryGetValue(key, out var value) ? value : null;
    }

    private sealed record HeaderMap(
        Dictionary<string, int> CanonicalIndexes,
        int? FirstNameIndex,
        int? LastNameIndex,
        string[] IgnoredColumns,
        bool HasDetails,
        string[] MissingRequiredColumns);
}
