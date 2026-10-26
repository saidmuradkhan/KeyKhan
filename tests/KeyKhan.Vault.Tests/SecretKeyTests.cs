using System.Security.Cryptography;
using System.Text;
using KeyKhan.Vault.Security;

namespace KeyKhan.Vault.Tests;

public sealed class SecretKeyTests
{
    private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Aad = "header"u8.ToArray();

    [Fact]
    public void Seal_then_open_round_trips()
    {
        using var key = SecretKey.Derive("correct horse", Salt, 1_000);
        byte[] plaintext = Encoding.UTF8.GetBytes("ünïcödé secret ✓");

        byte[] sealedData = key.Seal(plaintext, Aad);

        Assert.Equal(plaintext, key.Open(sealedData, Aad));
    }

    [Fact]
    public void Seal_uses_a_fresh_nonce_every_time()
    {
        using var key = SecretKey.Derive("password", Salt, 1_000);

        Assert.NotEqual(key.Seal("same"u8, Aad), key.Seal("same"u8, Aad));
    }

    [Fact]
    public void Open_with_wrong_password_fails()
    {
        using var key = SecretKey.Derive("password", Salt, 1_000);
        using var wrong = SecretKey.Derive("Password", Salt, 1_000);

        Assert.Throws<VaultAuthenticationException>(() => wrong.Open(key.Seal("data"u8, Aad), Aad));
    }

    [Fact]
    public void Open_detects_tampering()
    {
        using var key = SecretKey.Derive("password", Salt, 1_000);
        byte[] sealedData = key.Seal("data"u8, Aad);
        sealedData[14] ^= 0x01;

        Assert.Throws<VaultAuthenticationException>(() => key.Open(sealedData, Aad));
    }

    [Fact]
    public void Open_rejects_different_associated_data()
    {
        using var key = SecretKey.Derive("password", Salt, 1_000);

        Assert.Throws<VaultAuthenticationException>(() => key.Open(key.Seal("data"u8, Aad), "other"u8));
    }

    [Fact]
    public void Derive_from_SecureString_matches_span_overload()
    {
        using var fromSpan = SecretKey.Derive("pässwörd", Salt, 1_000);
        using var secure = Secrets.Of("pässwörd");
        using var fromSecure = SecretKey.Derive(secure, Salt, 1_000);

        Assert.Equal("ok"u8.ToArray(), fromSecure.Open(fromSpan.Seal("ok"u8, Aad), Aad));
    }
}
