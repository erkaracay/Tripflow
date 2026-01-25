using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Tripflow.Api.Features.Portal;

internal static class PortalAccessHelpers
{
    internal const int MaxAttempts = 5;
    internal static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    internal static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    internal static string BuildToken(Guid tokenId, string secret) => $"{tokenId:N}.{secret}";

    internal static bool TryParseToken(string? token, out Guid tokenId, out string secret)
    {
        tokenId = Guid.Empty;
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[0], "N", out tokenId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        secret = parts[1];
        return true;
    }

    internal static string HashSecret(string secret)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash);
    }

    internal static bool SecretMatches(string secret, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        byte[] storedBytes;
        try
        {
            storedBytes = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var provided = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(provided, storedBytes);
    }

    internal static string? BuildPhoneHint(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = ExtractDigits(phone);
        if (digits.Length < 2)
        {
            return null;
        }

        if (digits.StartsWith("90") && digits.Length == 12)
        {
            return $"+90 *** *** ** {digits[^2..]}";
        }

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        return $"***{last4}";
    }

    internal static string? ExtractLast4(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = ExtractDigits(value);
        if (digits.Length < 4)
        {
            return null;
        }

        return digits[^4..];
    }

    internal static string ExtractDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }
}
