using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Tripflow.Api.Helpers;

internal static class DataProtectionCertificateLoader
{
    private const string PrimaryPfxBase64Key = "DATA_PROTECTION_CERTIFICATE_PFX_BASE64";
    private const string PrimaryPasswordKey = "DATA_PROTECTION_CERTIFICATE_PASSWORD";
    private const string LegacyPfxBase64Key = "DATA_PROTECTION_LEGACY_CERTIFICATE_PFX_BASE64";
    private const string LegacyPasswordKey = "DATA_PROTECTION_LEGACY_CERTIFICATE_PASSWORD";

    internal static void Configure(
        IDataProtectionBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var primaryPfxBase64 = NormalizeBase64(configuration[PrimaryPfxBase64Key]);
        var primaryPassword = configuration[PrimaryPasswordKey];
        var legacyPfxBase64 = NormalizeBase64(configuration[LegacyPfxBase64Key]);
        var legacyPassword = configuration[LegacyPasswordKey];

        var primaryConfigured = !string.IsNullOrWhiteSpace(primaryPfxBase64);
        var legacyConfigured = !string.IsNullOrWhiteSpace(legacyPfxBase64) || !string.IsNullOrWhiteSpace(legacyPassword);

        if (!primaryConfigured)
        {
            if (legacyConfigured)
            {
                throw new InvalidOperationException(
                    $"{LegacyPfxBase64Key} / {LegacyPasswordKey} cannot be set unless {PrimaryPfxBase64Key} is also configured.");
            }

            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Missing Data Protection certificate. Set {PrimaryPfxBase64Key} and {PrimaryPasswordKey} in non-development environments.");
            }

            return;
        }

        var primaryCertificate = LoadCertificate(primaryPfxBase64!, primaryPassword, PrimaryPfxBase64Key, PrimaryPasswordKey);
        builder.ProtectKeysWithCertificate(primaryCertificate);

        if (!legacyConfigured)
            return;

        if (string.IsNullOrWhiteSpace(legacyPfxBase64))
        {
            throw new InvalidOperationException(
                $"{LegacyPfxBase64Key} must be provided when configuring a legacy Data Protection certificate.");
        }

        var legacyCertificate = LoadCertificate(legacyPfxBase64, legacyPassword, LegacyPfxBase64Key, LegacyPasswordKey);
        builder.UnprotectKeysWithAnyCertificate(primaryCertificate, legacyCertificate);
    }

    private static X509Certificate2 LoadCertificate(
        string pfxBase64,
        string? password,
        string pfxKeyName,
        string passwordKeyName)
    {
        byte[] pfxBytes;
        try
        {
            pfxBytes = Convert.FromBase64String(pfxBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{pfxKeyName} is not valid base64.", ex);
        }

        try
        {
            var certificate = LoadWithPreferredKeyStorageFlags(pfxBytes, password);

            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException($"{pfxKeyName} must contain a certificate with a private key.");
            }

            return certificate;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Failed to load certificate from {pfxKeyName}. Check that the PFX is valid and {passwordKeyName} matches the PFX password.",
                ex);
        }
    }

    private static X509Certificate2 LoadWithPreferredKeyStorageFlags(byte[] pfxBytes, string? password)
    {
        try
        {
            return new X509Certificate2(
                pfxBytes,
                password ?? string.Empty,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (PlatformNotSupportedException)
        {
            // macOS does not support EphemeralKeySet for PKCS#12 loading.
            return new X509Certificate2(pfxBytes, password ?? string.Empty);
        }
    }

    private static string? NormalizeBase64(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
