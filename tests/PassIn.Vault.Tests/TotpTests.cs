using PassIn.Vault.Otp;

namespace PassIn.Vault.Tests;

public sealed class TotpTests
{
    private const string Sha1Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private const string Sha256Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZA";
    private const string Sha512Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNA";

    // RFC 6238, Appendix B.
    [Theory]
    [InlineData(59L, TotpAlgorithm.Sha1, "94287082")]
    [InlineData(59L, TotpAlgorithm.Sha256, "46119246")]
    [InlineData(59L, TotpAlgorithm.Sha512, "90693936")]
    [InlineData(1111111109L, TotpAlgorithm.Sha1, "07081804")]
    [InlineData(1234567890L, TotpAlgorithm.Sha256, "91819424")]
    [InlineData(2000000000L, TotpAlgorithm.Sha512, "38618901")]
    [InlineData(20000000000L, TotpAlgorithm.Sha1, "65353130")]
    public void Generate_matches_rfc_vectors(long unixSeconds, TotpAlgorithm algorithm, string expected)
    {
        string seed = algorithm switch
        {
            TotpAlgorithm.Sha256 => Sha256Seed,
            TotpAlgorithm.Sha512 => Sha512Seed,
            _ => Sha1Seed,
        };
        var settings = new TotpSettings(seed, Digits: 8, Algorithm: algorithm);

        var code = TotpGenerator.Generate(settings, DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, code.Code);
    }

    [Fact]
    public void Generate_reports_seconds_remaining()
    {
        var code = TotpGenerator.Generate(new TotpSettings(Sha1Seed), DateTimeOffset.FromUnixTimeSeconds(65));

        Assert.Equal(25, code.SecondsRemaining);
        Assert.Equal(6, code.Code.Length);
    }

    [Fact]
    public void TryParse_reads_otpauth_uri()
    {
        const string uri = "otpauth://totp/Example:alice@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Example&digits=8&period=60&algorithm=SHA256";

        Assert.True(TotpSettings.TryParse(uri, out var settings));
        Assert.Equal(new TotpSettings("JBSWY3DPEHPK3PXP", 8, 60, TotpAlgorithm.Sha256), settings);
    }

    [Theory]
    [InlineData("jbsw y3dp ehpk 3pxp")]
    [InlineData("JBSWY3DPEHPK3PXP====")]
    public void TryParse_accepts_formatted_base32(string secret) =>
        Assert.True(TotpSettings.TryParse(secret, out _));

    [Theory]
    [InlineData("")]
    [InlineData("not base32!")]
    [InlineData("otpauth://hotp/x?secret=JBSWY3DPEHPK3PXP")]
    [InlineData("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&digits=12")]
    public void TryParse_rejects_invalid_input(string value) =>
        Assert.False(TotpSettings.TryParse(value, out _));
}
