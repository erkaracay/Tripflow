namespace Tripflow.Api.Helpers;

/// <summary>
/// Helper to build QR code payload for participant check-in links.
/// Matches the frontend portal QR format exactly.
/// </summary>
internal static class QrPayloadHelper
{
    /// <summary>
    /// Builds the guide check-in link URL that matches the portal QR format.
    /// Format: {baseUrl}/guide/events/{eventId}/checkin?code={checkInCode}
    /// </summary>
    internal static string BuildGuideCheckInLink(string baseUrl, Guid eventId, string checkInCode)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        var encodedCode = Uri.EscapeDataString(checkInCode);
        return $"{normalizedBase}/guide/events/{eventId}/checkin?code={encodedCode}";
    }
}
