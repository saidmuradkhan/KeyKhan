namespace PassIn.Vault.Tests;

public sealed class FolderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("passin-tests-").FullName;

    private string VaultPath => Path.Combine(_directory, "vault.passin");

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
    public void Folder_style_survives_reopening_and_follows_a_rename()
    {
        using (var vault = CreateVault())
        {
            vault.AddFolder("Work");
            vault.SetFolderStyle("work", new FolderStyle("briefcase", "teal"));
            vault.RenameFolder("Work", "Client A");
        }

        using var reopened = Reopen();
        Assert.Equal(new FolderStyle("briefcase", "teal"), reopened.StyleOf("client a"));
        Assert.Equal(FolderStyle.Default, reopened.StyleOf("Work"));
    }

    [Fact]
    public void Deleting_a_folder_forgets_its_style()
    {
        using var vault = CreateVault();
        vault.AddFolder("Work");
        vault.SetFolderStyle("Work", new FolderStyle("star", ""));

        vault.DeleteFolder("Work");
        vault.AddFolder("Work");

        Assert.Equal(FolderStyle.Default, vault.StyleOf("Work"));
    }

    [Fact]
    public void Entry_icon_and_color_are_saved()
    {
        using (var vault = CreateVault())
        {
            vault.Upsert(new VaultEntry { Title = "Bank", Icon = "bank", Color = "green" });
        }

        using var reopened = Reopen();
        var entry = Assert.Single(reopened.Entries);
        Assert.Equal(("bank", "green"), (entry.Icon, entry.Color));
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
