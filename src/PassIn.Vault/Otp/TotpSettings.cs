using System.Diagnostics.CodeAnalysis;
using System.Web;

namespace PassIn.Vault.Otp;

public enum TotpAlgorithm
{
    Sha1,
    Sha256,
    Sha512,
}

public sealed record TotpSettings(string Secret, int Digits = 6, int Period = 30, TotpAlgorithm Algorithm = TotpAlgorithm.Sha1)
{
    /// <summary>Accepts a bare Base32 secret or an <c>otpauth://totp/...</c> URI.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out TotpSettings? settings)
    {
        settings = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (!value.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            settings = IsBase32(value) ? new TotpSettings(value) : null;
            return settings is not null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.Host.Equals("totp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        string secret = query["secret"] ?? "";
        int digits = int.TryParse(query["digits"], out int d) ? d : 6;
        int period = int.TryParse(query["period"], out int p) ? p : 30;
        TotpAlgorithm? algorithm = (query["algorithm"] ?? "SHA1").ToUpperInvariant() switch
        {
            "SHA1" => TotpAlgorithm.Sha1,
            "SHA256" => TotpAlgorithm.Sha256,
            "SHA512" => TotpAlgorithm.Sha512,
            _ => null,
        };

        if (!IsBase32(secret) || digits is < 6 or > 8 || period <= 0 || algorithm is null)
        {
            return false;
        }

        settings = new TotpSettings(secret, digits, period, algorithm.Value);
        return true;
    }

    private static bool IsBase32(string value)
    {
        bool hasSymbol = false;
        foreach (char c in value)
        {
            if (c is ' ' or '-' or '=')
            {
                continue;
            }
            if (!char.IsAsciiLetter(c) && c is not (>= '2' and <= '7'))
            {
                return false;
            }
            hasSymbol = true;
        }
        return hasSymbol;
    }
}
