using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KeyKhan.Vault.Security;

namespace KeyKhan.Vault.Tests;

public sealed class LegacyUpgradeTests : IDisposable
{
    private const string MasterPassword = "master-password";

    private readonly string _directory = Directory.CreateTempSubdirectory("keykhan-tests-").FullName;

    private string VaultPath => Path.Combine(_directory, "vault.keykhan");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Opening_a_1_0_vault_upgrades_it_and_keeps_a_backup()
    {
        WriteLegacyVault("""{"entries":[{"id":"8c5b0f4e-2b9a-4f5e-9d1a-0c1e2f3a4b5c","title":"GitHub","password":"s3cret"}]}""");

        string recoveryKey;
        using (var vault = PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword)))
        {
            Assert.Equal("s3cret", Assert.Single(vault.Entries).Password);
            recoveryKey = Assert.IsType<string>(vault.NewRecoveryKey);
        }

        Assert.True(File.Exists(VaultPath + ".v1.bak"));
        using (var reopened = PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword)))
        {
            Assert.Null(reopened.NewRecoveryKey);
            Assert.Single(reopened.Entries);
        }
        PasswordVault.Recover(VaultPath, recoveryKey, Secrets.Of("new-password")).Dispose();
    }

    [Fact]
    public void Wrong_password_leaves_a_1_0_vault_untouched()
    {
        WriteLegacyVault("""{"entries":[]}""");
        byte[] before = File.ReadAllBytes(VaultPath);

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of("wrong-password")));

        Assert.Equal(before, File.ReadAllBytes(VaultPath));
        Assert.False(File.Exists(VaultPath + ".v1.bak"));
    }

    // Byte-for-byte what KeyKhan 1.0 wrote: the password-derived key sealed the payload, the header was the AAD.
    private void WriteLegacyVault(string payloadJson)
    {
        const int iterations = 1_000;
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        var header = new byte[12 + salt.Length];
        "KKVT"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), 1);
        header[6] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(7), iterations);
        header[11] = (byte)salt.Length;
        salt.CopyTo(header, 12);

        using var key = SecretKey.Derive(MasterPassword, salt, iterations);
        File.WriteAllBytes(VaultPath, [.. header, .. key.Seal(Encoding.UTF8.GetBytes(payloadJson), header)]);
    }
}
