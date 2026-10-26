namespace KeyKhan.Vault.Interchange;

/// <summary>
/// Imports CSV exports from common password managers and browsers
/// (Chrome, Edge, Firefox, Bitwarden, LastPass, 1Password, KeePass) by recognising their column names.
/// </summary>
public static class CsvInterchange
{
    private static readonly string[] ExportColumns = ["title", "username", "password", "url", "folder", "notes", "totp", "favorite"];

    private static readonly Dictionary<string, string[]> ColumnAliases = new()
    {
        ["title"] = ["title", "name", "account", "entry"],
        ["username"] = ["username", "login_username", "user name", "login name", "login", "email"],
        ["password"] = ["password", "login_password"],
        ["url"] = ["url", "login_uri", "website", "web site", "uri"],
        ["folder"] = ["folder", "grouping", "group", "category"],
        ["notes"] = ["notes", "note", "extra", "comments"],
        ["totp"] = ["totp", "login_totp", "otpauth", "otp"],
        ["favorite"] = ["favorite", "fav"],
    };

    public static IReadOnlyList<VaultEntry> Import(TextReader reader)
    {
        using var records = CsvFormat.Read(reader).GetEnumerator();
        if (!records.MoveNext())
        {
            return [];
        }

        var columns = MapColumns(records.Current);
        if (!columns.ContainsKey("password"))
        {
            throw new InvalidDataException("The CSV file has no password column.");
        }

        var entries = new List<VaultEntry>();
        while (records.MoveNext())
        {
            string[] record = records.Current;
            string Field(string name) => columns.TryGetValue(name, out int i) && i < record.Length ? record[i].Trim() : "";

            string url = Field("url");
            string username = Field("username");
            string title = FirstNonEmpty(Field("title"), HostOf(url), username);
            if (title.Length == 0 && Field("password").Length == 0)
            {
                continue;
            }

            entries.Add(new VaultEntry
            {
                Title = title.Length > 0 ? title : "Untitled",
                Username = username,
                Password = Field("password"),
                Url = url,
                Folder = Field("folder"),
                Notes = Field("notes"),
                Totp = Field("totp"),
                IsFavorite = Field("favorite") is "1" or "true" or "True" or "TRUE",
            });
        }
        return entries;
    }

    public static void Export(IEnumerable<VaultEntry> entries, TextWriter writer)
    {
        CsvFormat.WriteRecord(writer, ExportColumns);
        foreach (var e in entries)
        {
            CsvFormat.WriteRecord(writer, [e.Title, e.Username, e.Password, e.Url, e.Folder, e.Notes, e.Totp, e.IsFavorite ? "1" : "0"]);
        }
    }

    private static Dictionary<string, int> MapColumns(string[] header)
    {
        var map = new Dictionary<string, int>();
        for (int i = 0; i < header.Length; i++)
        {
            string column = header[i].Trim().TrimStart('﻿').ToLowerInvariant();
            foreach (var (field, aliases) in ColumnAliases)
            {
                if (aliases.Contains(column))
                {
                    map.TryAdd(field, i);
                }
            }
        }
        return map;
    }

    private static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : "";

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => v.Length > 0) ?? "";
}
