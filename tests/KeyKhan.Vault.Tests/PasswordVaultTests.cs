namespace KeyKhan.Vault.Tests;

public sealed class PasswordVaultTests : IDisposable
{
    private const int FastIterations = 1_000;

    private readonly string _directory = Directory.CreateTempSubdirectory("keykhan-tests-").FullName;

    private string VaultPath => Path.Combine(_directory, "vault.keykhan");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Entries_survive_reopening()
    {
        using (var password = Secrets.Of("master-password"))
        using (var vault = PasswordVault.Create(VaultPath, password, FastIterations))
        {
            vault.Upsert(new VaultEntry { Title = "GitHub", Username = "octo", Password = "s3cret" });
        }

        using var reopened = PasswordVault.Open(VaultPath, Secrets.Of("master-password"));

        var entry = Assert.Single(reopened.Entries);
        Assert.Equal(("GitHub", "octo", "s3cret"), (entry.Title, entry.Username, entry.Password));
    }

    [Fact]
    public void Open_with_wrong_password_throws()
    {
        PasswordVault.Create(VaultPath, Secrets.Of("master-password"), FastIterations).Dispose();

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of("wrong-password")));
    }

    [Fact]
    public void Upsert_replaces_existing_entry_by_id()
    {
        using var vault = PasswordVault.Create(VaultPath, Secrets.Of("master-password"), FastIterations);
        var entry = new VaultEntry { Title = "Mail" };
        vault.Upsert(entry);

        vault.Upsert(entry with { Title = "Work mail" });

        Assert.Equal("Work mail", Assert.Single(vault.Entries).Title);
    }

    [Fact]
    public void Remove_deletes_entry()
    {
        using var vault = PasswordVault.Create(VaultPath, Secrets.Of("master-password"), FastIterations);
        var entry = new VaultEntry { Title = "Bank" };
        vault.Upsert(entry);

        vault.Remove(entry.Id);

        Assert.Empty(vault.Entries);
    }

    [Fact]
    public void ChangeMasterPassword_rekeys_the_file()
    {
        using (var vault = PasswordVault.Create(VaultPath, Secrets.Of("old-password"), FastIterations))
        {
            vault.Upsert(new VaultEntry { Title = "Entry" });
            vault.ChangeMasterPassword(Secrets.Of("old-password"), Secrets.Of("new-password"));
        }

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of("old-password")));
        using var reopened = PasswordVault.Open(VaultPath, Secrets.Of("new-password"));
        Assert.Single(reopened.Entries);
    }

    [Fact]
    public void ChangeMasterPassword_requires_current_password()
    {
        using var vault = PasswordVault.Create(VaultPath, Secrets.Of("old-password"), FastIterations);

        Assert.Throws<VaultAuthenticationException>(
            () => vault.ChangeMasterPassword(Secrets.Of("not-it"), Secrets.Of("new-password")));
    }

    [Fact]
    public void Tampered_header_is_rejected()
    {
        PasswordVault.Create(VaultPath, Secrets.Of("master-password"), FastIterations).Dispose();
        byte[] content = File.ReadAllBytes(VaultPath);
        content[20] ^= 0xFF; // inside the salt
        File.WriteAllBytes(VaultPath, content);

        Assert.Throws<VaultAuthenticationException>(() => PasswordVault.Open(VaultPath, Secrets.Of("master-password")));
    }

    [Fact]
    public void Non_vault_file_is_rejected()
    {
        File.WriteAllText(VaultPath, "definitely not a vault");

        Assert.Throws<InvalidDataException>(() => PasswordVault.Open(VaultPath, Secrets.Of("master-password")));
    }
}
