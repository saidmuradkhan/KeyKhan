using System.Text.Json;
using System.Text.Json.Nodes;
using PassIn.App.Services;

namespace PassIn.App;

/// <summary>
/// PassIn was called KeyKhan up to version 1.2. Moves that version's files and autostart entry over once,
/// so updating keeps the vault. Every step can be repeated, so a run that fails halfway finishes on the next start.
/// </summary>
internal static class LegacyMigration
{
    private const string LegacyName = "KeyKhan";
    private const string LegacyVaultFileName = "vault.keykhan";

    /// <returns><c>false</c> if the old files could not be moved, e.g. because KeyKhan is still running.</returns>
    public static bool Run()
    {
        // A PASSIN_HOME profile was set up after the rename and has nothing to move.
        if (!AppPaths.IsDefaultProfile)
        {
            return true;
        }

        try
        {
            string oldData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyName);
            MoveDirectory(oldData, AppPaths.DataDirectory);
            MoveDefaultVault(oldData);

            string oldLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyName);
            MoveDirectory(oldLocal, Path.GetDirectoryName(AppPaths.DeviceKeyFile)!);

            StartupRegistration.ReplaceLegacy(LegacyName);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static void MoveDirectory(string from, string to)
    {
        if (Directory.Exists(from) && !Directory.Exists(to))
        {
            Directory.Move(from, to);
        }
    }

    // Renames vault.keykhan to vault.passin and points settings.json at the new folder. A vault kept
    // elsewhere (e.g. in OneDrive) stays where it is.
    private static void MoveDefaultVault(string oldData)
    {
        string oldVault = Path.Combine(AppPaths.DataDirectory, LegacyVaultFileName);
        var settings = File.Exists(AppPaths.SettingsFile) ? JsonNode.Parse(File.ReadAllText(AppPaths.SettingsFile)) as JsonObject : null;
        string? vaultPath = settings?["VaultPath"]?.GetValue<string>();

        if (vaultPath is not null && IsInside(vaultPath, oldData))
        {
            vaultPath = Path.Join(AppPaths.DataDirectory, Path.GetRelativePath(oldData, vaultPath));
        }

        if (vaultPath is null || SamePath(vaultPath, oldVault))
        {
            if (File.Exists(oldVault) && !File.Exists(AppPaths.DefaultVaultFile))
            {
                File.Move(oldVault, AppPaths.DefaultVaultFile);
            }
            vaultPath = settings is null ? null : AppPaths.DefaultVaultFile;
        }

        if (settings is not null && vaultPath is not null && vaultPath != settings["VaultPath"]?.GetValue<string>())
        {
            settings["VaultPath"] = vaultPath;
            File.WriteAllText(AppPaths.SettingsFile, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static bool IsInside(string path, string directory) =>
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
