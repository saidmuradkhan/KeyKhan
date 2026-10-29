using PassIn.Vault.Interop;

namespace PassIn.Vault.Otp;

public readonly record struct TotpCode(string Code, int SecondsRemaining, int Period);

public static class TotpGenerator
{
    public static unsafe TotpCode Generate(TotpSettings settings, DateTimeOffset now)
    {
        long unixSeconds = now.ToUnixTimeSeconds();
        var algorithm = settings.Algorithm switch
        {
            TotpAlgorithm.Sha256 => NativeHash.Sha256,
            TotpAlgorithm.Sha512 => NativeHash.Sha512,
            _ => NativeHash.Sha1,
        };

        uint code;
        fixed (char* secret = settings.Secret)
        {
            NativeMethods.kk_totp(
                    secret, (nuint)settings.Secret.Length, unixSeconds, (uint)settings.Period, (uint)settings.Digits, algorithm, out code)
                .ThrowIfFailed();
        }

        int remaining = settings.Period - (int)(unixSeconds % settings.Period);
        return new TotpCode(code.ToString().PadLeft(settings.Digits, '0'), remaining, settings.Period);
    }
}
