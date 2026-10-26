using KeyKhan.Vault.Security;

namespace KeyKhan.Vault.Tests;

public sealed class PasswordVaultTests : IDisposable
{
    private const int FastIterations = 1_000;
    private const string MasterPassword = "master-password";

    private readonly string _directory = Directory.CreateTempSubdirectory("keykhan-tests-").FullName;

    private string VaultPath => Path.Combine(_directory, "vault.keykhan");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private PasswordVault CreateVault(out string recoveryKey) =>
        PasswordVault.Create(VaultPath, Secrets.Of(MasterPassword), FastIterations, out recoveryKey);

    private PasswordVault CreateVault() => CreateVault(out _);

    [Fact]
    public void Entries_survive_reopening()
    {
        using (var vault = CreateVault())
        {
            vault.Upsert(new VaultEntry { Title = "GitHub", Username = "octo", Password = "s3cret" });
        }

        using var reopened = PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword));

        var entry = Assert.Single(reopened.Entries);
        Assert.Equal(("GitHub", "octo", "s3cret"), (entry.Title, entry.Username, entry.Password));
    }

    [Fact]
    public void Open_with_wrong_password_throws()
    {
        CreateVault().Dispose();

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of("wrong-password")));
    }

    [Fact]
    public void Upsert_replaces_existing_entry_by_id()
    {
        using var vault = CreateVault();
        var entry = new VaultEntry { Title = "Mail" };
        vault.Upsert(entry);

        vault.Upsert(entry with { Title = "Work mail" });

        Assert.Equal("Work mail", Assert.Single(vault.Entries).Title);
    }

    [Fact]
    public void Remove_deletes_entry()
    {
        using var vault = CreateVault();
        var entry = new VaultEntry { Title = "Bank" };
        vault.Upsert(entry);

        vault.Remove(entry.Id);

        Assert.Empty(vault.Entries);
    }

    [Fact]
    public void ChangeMasterPassword_replaces_the_password()
    {
        using (var vault = CreateVault())
        {
            vault.Upsert(new VaultEntry { Title = "Entry" });
            vault.ChangeMasterPassword(Secrets.Of(MasterPassword), Secrets.Of("new-password"));
        }

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword)));
        using var reopened = PasswordVault.Open(VaultPath, Secrets.Of("new-password"));
        Assert.Single(reopened.Entries);
    }

    [Fact]
    public void ChangeMasterPassword_requires_current_password()
    {
        using var vault = CreateVault();

        Assert.Throws<VaultAuthenticationException>(
            () => vault.ChangeMasterPassword(Secrets.Of("not-it"), Secrets.Of("new-password")));
    }

    [Fact]
    public void Recovery_key_resets_a_forgotten_password()
    {
        using (var vault = CreateVault(out string recoveryKey))
        {
            vault.Upsert(new VaultEntry { Title = "Entry" });
            vault.Dispose();

            using var recovered = PasswordVault.Recover(VaultPath, recoveryKey.ToLowerInvariant().Replace("-", " "), Secrets.Of("fresh-password"));
            Assert.Single(recovered.Entries);
        }

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword)));
        using var reopened = PasswordVault.Open(VaultPath, Secrets.Of("fresh-password"));
        Assert.Single(reopened.Entries);
    }

    [Theory]
    [InlineData("AAAA-AAAA-AAAA-AAAA-AAAA-AAAA-AAAA-AAAA")]
    [InlineData("not a key")]
    public void Recover_rejects_wrong_keys(string recoveryKey)
    {
        CreateVault().Dispose();

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Recover(VaultPath, recoveryKey, Secrets.Of("x-password")));
    }

    [Fact]
    public void RegenerateRecoveryKey_revokes_the_old_key()
    {
        string newKey;
        using (var vault = CreateVault(out string oldKey))
        {
            newKey = vault.RegenerateRecoveryKey();
            Assert.NotEqual(oldKey, newKey);
            vault.Dispose();
            Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Recover(VaultPath, oldKey, Secrets.Of("x-password")));
        }

        PasswordVault.Recover(VaultPath, newKey, Secrets.Of("x-password")).Dispose();
    }

    [Fact]
    public void Device_key_opens_the_vault_without_a_password()
    {
        byte[] deviceKey;
        using (var vault = CreateVault())
        {
            vault.Upsert(new VaultEntry { Title = "Entry" });
            deviceKey = vault.CreateDeviceKey();
        }

        using var reopened = PasswordVault.OpenWithDeviceKey(VaultPath, deviceKey);

        Assert.Single(reopened.Entries);
    }

    [Fact]
    public void Device_key_of_another_vault_is_rejected()
    {
        byte[] foreignKey;
        string otherPath = Path.Combine(_directory, "other.keykhan");
        using (var other = PasswordVault.Create(otherPath, Secrets.Of(MasterPassword), FastIterations, out _))
        {
            foreignKey = other.CreateDeviceKey();
        }
        CreateVault().Dispose();

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.OpenWithDeviceKey(VaultPath, foreignKey));
    }

    [Fact]
    public void Tampered_header_is_rejected()
    {
        CreateVault().Dispose();
        byte[] content = File.ReadAllBytes(VaultPath);
        content[20] ^= 0xFF; // inside the password slot's salt
        File.WriteAllBytes(VaultPath, content);

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword)));
    }

    [Fact]
    public void Non_vault_file_is_rejected()
    {
        File.WriteAllText(VaultPath, "definitely not a vault");

        Assert.Throws<InvalidDataException>(() => PasswordVault.Open(VaultPath, Secrets.Of(MasterPassword)));
    }

    [Fact]
    public void Recovery_keys_are_well_formed_and_unique()
    {
        string first = RecoveryKey.Generate();

        Assert.Matches("^([A-Z2-7]{4}-){7}[A-Z2-7]{4}$", first);
        Assert.NotEqual(first, RecoveryKey.Generate());
        Assert.Equal(first.Replace("-", ""), RecoveryKey.Normalize(first.ToLowerInvariant()));
        Assert.Equal("OIB" + new string('A', 29), RecoveryKey.Normalize("01b-" + new string('a', 29)));
    }
}
