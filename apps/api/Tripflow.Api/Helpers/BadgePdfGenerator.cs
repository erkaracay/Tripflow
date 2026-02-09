using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Helpers;

internal static class BadgePdfGenerator
{
    private const float PageWidthMm = 90f;
    private const float PageHeightMm = 120f;
    private const float OuterPaddingMm = 6f;
    private const float QrSizeMm = 42f;

    private const float HeaderHeightMm = 22f;
    private const float HeaderLogoBoxMm = 18f;

    private static class BadgeTheme
    {
        public const string Brand = "#2563EB";
        public const string Text = "#111827";
        public const string Muted = "#6B7280";
        public const string Card = "#F3F4F6";
        public const string Border = "#E5E7EB";
        public const string HeaderLogoBorder = "#59FFFFFF";
    }

    static BadgePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    internal static byte[] GenerateBadgesPdf(
        EventEntity eventEntity,
        IList<ParticipantEntity> participants,
        string publicBaseUrl)
    {
        var filtered = participants
            .Where(p => !p.WillNotAttend)
            .OrderBy(p => p.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.TcNo)
            .ToList();

        var eventTitle = eventEntity.Name;
        var dateRange = FormatDateRange(eventEntity.StartDate, eventEntity.EndDate);

        if (filtered.Count == 0)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageWidthMm, PageHeightMm, Unit.Millimetre);
                    page.Margin(0);
                    page.Content().Background(Colors.White);
                });
            }).GeneratePdf();
        }

        return Document.Create(container =>
        {
            foreach (var participant in filtered)
            {
                container.Page(page =>
                {
                    page.Size(PageWidthMm, PageHeightMm, Unit.Millimetre);
                    page.Margin(0);

                    page.Content()
                        .Background(Colors.White)
                        .Column(col =>
                        {
                            // HEADER BAND
                            col.Item()
                                .Height(HeaderHeightMm, Unit.Millimetre)
                                .Background(BadgeTheme.Brand)
                                .PaddingHorizontal(OuterPaddingMm, Unit.Millimetre)
                                .PaddingVertical(4)
                                .Row(r =>
                                {
                                    r.RelativeItem().Column(h =>
                                    {
                                        h.Spacing(2);

                                        h.Item().PaddingTop(10).Text(eventTitle)
                                            .FontColor(Colors.White)
                                            .FontSize(13)
                                            .Bold()
                                            .FontFamily(Fonts.Calibri)
                                            .LineHeight(1.1f);

                                        h.Item().Text(dateRange)
                                            .FontColor(Colors.White)
                                            .FontSize(10.5f)
                                            .FontFamily(Fonts.Calibri)
                                            .LineHeight(1.2f);
                                    });
                                });

                            // BODY
                            col.Item()
                                .Padding(OuterPaddingMm, Unit.Millimetre)
                                .Column(body =>
                                {
                                    body.Spacing(6);

                                    // NAME
                                    var (nameFontSize, nameLines) = CalculateNameLayout(participant.FullName);

                                    body.Item()
                                        .AlignCenter()
                                        .PaddingTop(2)
                                        .Text(t =>
                                        {
                                            if (nameLines.Length == 1)
                                            {
                                                t.Span(nameLines[0])
                                                    .FontSize(nameFontSize + 2)
                                                    .Bold()
                                                    .FontFamily(Fonts.Calibri)
                                                    .FontColor(BadgeTheme.Text)
                                                    .LineHeight(1.1f);
                                            }
                                            else
                                            {
                                                t.Span(nameLines[0])
                                                    .FontSize(nameFontSize + 1)
                                                    .Bold()
                                                    .FontFamily(Fonts.Calibri)
                                                    .FontColor(BadgeTheme.Text)
                                                    .LineHeight(1.1f);

                                                t.Span("\n");

                                                t.Span(nameLines[1])
                                                    .FontSize(nameFontSize + 1)
                                                    .Bold()
                                                    .FontFamily(Fonts.Calibri)
                                                    .FontColor(BadgeTheme.Text)
                                                    .LineHeight(1.1f);
                                            }
                                        });

                                    body.Item().PaddingTop(4);

                                    var qrPayload = QrPayloadHelper.BuildGuideCheckInLink(
                                        publicBaseUrl,
                                        eventEntity.Id,
                                        participant.CheckInCode);

                                    var qrBytes = GenerateQrCode(qrPayload, QrSizeMm);

                                    // QR CARD (no rounded corners)
                                    body.Item().AlignCenter()
                                        .Background(BadgeTheme.Card)
                                        .Border(1)
                                        .BorderColor(BadgeTheme.Border)
                                        .Padding(10)
                                        .Column(q =>
                                        {
                                            q.Spacing(6);

                                            q.Item().AlignCenter()
                                                .Width(QrSizeMm, Unit.Millimetre)
                                                .Height(QrSizeMm, Unit.Millimetre)
                                                .Image(qrBytes)
                                                .FitArea();
                                        });

                                    // Bottom accent bar
                                    body.Item()
                                        .PaddingTop(4)
                                        .Height(3)
                                        .Background(BadgeTheme.Brand);
                                });
                    });
                });
            }
        }).GeneratePdf();
    }

    private static string FormatDateRange(DateOnly start, DateOnly end)
    {
        var startStr = start.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var endStr = end.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        return $"{startStr} – {endStr}";
    }

    private static (float fontSize, string[] lines) CalculateNameLayout(string name)
    {
        const float defaultFontSize = 26f;
        const float reducedFontSize = 22f;
        const float minFontSize = 18f;

        if (name.Length <= 20)
            return (defaultFontSize, new[] { name });

        if (name.Length <= 28)
            return (reducedFontSize, new[] { name });

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            var midPoint = words.Length / 2;
            var line1 = string.Join(" ", words.Take(midPoint));
            var line2 = string.Join(" ", words.Skip(midPoint));

            var maxLineLength = Math.Max(line1.Length, line2.Length);

            if (maxLineLength <= 18)
                return (defaultFontSize, new[] { line1, line2 });

            if (maxLineLength <= 22)
                return (reducedFontSize, new[] { line1, line2 });
        }

        return (minFontSize, new[] { name });
    }

    private static byte[] GenerateQrCode(string payload, float sizeMm)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);

        var targetPixels = (int)(sizeMm * 300f / 25.4f);
        var pixelPerModule = Math.Max(10, targetPixels / 35);

        return qrCode.GetGraphic(pixelPerModule, drawQuietZones: true);
    }
}
