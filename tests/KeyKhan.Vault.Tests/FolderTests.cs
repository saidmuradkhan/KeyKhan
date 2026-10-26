namespace KeyKhan.Vault.Tests;

public sealed class FolderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keykhan-tests-").FullName;

    private string VaultPath => Path.Combine(_directory, "vault.keykhan");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private PasswordVault CreateVault() => PasswordVault.Create(VaultPath, Secrets.Of("master-password"), 1_000, out _);

    private PasswordVault Reopen() => PasswordVault.Open(VaultPath, Secrets.Of("master-password"));

    [Fact]
    public void Empty_folders_are_kept()
    {
        using (var vault = CreateVault())
        {
            vault.AddFolder("  Project X ");
            vault.AddFolder("project x");
        }

        using var reopened = Reopen();
        Assert.Equal(["Project X"], reopened.Folders);
    }

    [Fact]
    public void Folders_include_those_used_by_entries()
    {
        using var vault = CreateVault();
        vault.AddFolder("Work");
        vault.Upsert(new VaultEntry { Title = "Bank", Folder = "Finance" });

        Assert.Equal(["Finance", "Work"], vault.Folders);
    }

    [Fact]
    public void Rename_moves_entries()
    {
        using (var vault = CreateVault())
        {
            vault.Upsert(new VaultEntry { Title = "Jira", Folder = "Work" });
            vault.Upsert(new VaultEntry { Title = "Mail", Folder = "Personal" });
            vault.RenameFolder("work", "Client A");
        }

        using var reopened = Reopen();
        Assert.Equal(["Client A", "Personal"], reopened.Folders);
        Assert.Equal("Client A", reopened.Entries.Single(e => e.Title == "Jira").Folder);
    }

    [Fact]
    public void Delete_keeps_entries_without_a_folder()
    {
        using var vault = CreateVault();
        vault.Upsert(new VaultEntry { Title = "Jira", Folder = "Work" });

        vault.DeleteFolder("Work");

        Assert.Empty(vault.Folders);
        Assert.Equal("", Assert.Single(vault.Entries).Folder);
    }
}
