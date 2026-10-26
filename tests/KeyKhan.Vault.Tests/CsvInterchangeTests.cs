using KeyKhan.Vault.Interchange;

namespace KeyKhan.Vault.Tests;

public sealed class CsvInterchangeTests
{
    [Fact]
    public void Imports_chrome_export()
    {
        const string csv = "name,url,username,password,note\r\nGitHub,https://github.com/login,octo,\"pa,ss\"\"word\",\"line1\nline2\"\r\n";

        var entry = Assert.Single(CsvInterchange.Import(new StringReader(csv)));

        Assert.Equal("GitHub", entry.Title);
        Assert.Equal("https://github.com/login", entry.Url);
        Assert.Equal("octo", entry.Username);
        Assert.Equal("pa,ss\"word", entry.Password);
        Assert.Equal("line1\nline2", entry.Notes);
    }

    [Fact]
    public void Imports_bitwarden_export()
    {
        const string csv = """
            folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp
            Work,1,login,Jira,,,0,https://jira.example.com,me@example.com,hunter2,JBSWY3DPEHPK3PXP
            """;

        var entry = Assert.Single(CsvInterchange.Import(new StringReader(csv)));

        Assert.Equal(("Work", true, "Jira", "hunter2", "JBSWY3DPEHPK3PXP"),
            (entry.Folder, entry.IsFavorite, entry.Title, entry.Password, entry.Totp));
    }

    [Fact]
    public void Falls_back_to_host_when_title_is_missing()
    {
        const string csv = "url,username,password\nhttps://accounts.example.org/signin,bob,pw\n";

        Assert.Equal("accounts.example.org", Assert.Single(CsvInterchange.Import(new StringReader(csv))).Title);
    }

    [Fact]
    public void Rejects_files_without_password_column() =>
        Assert.Throws<InvalidDataException>(() => CsvInterchange.Import(new StringReader("a,b\n1,2\n")));

    [Fact]
    public void Export_then_import_round_trips()
    {
        var original = new VaultEntry
        {
            Title = "Bank, main",
            Username = "user",
            Password = "p\"w",
            Url = "https://bank.example",
            Folder = "Finance",
            Notes = "multi\nline",
            Totp = "JBSWY3DPEHPK3PXP",
            IsFavorite = true,
        };
        var writer = new StringWriter();

        CsvInterchange.Export([original], writer);
        var imported = Assert.Single(CsvInterchange.Import(new StringReader(writer.ToString())));

        Assert.Equal(original with { Id = imported.Id, CreatedAt = imported.CreatedAt, ModifiedAt = imported.ModifiedAt }, imported);
    }
}
