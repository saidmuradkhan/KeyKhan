using System.Security.Cryptography;
using System.Text;

namespace KeyKhan.App;

internal static class AppPaths
{
    // KEYKHAN_HOME keeps everything in one folder instead, e.g. for a second profile or for development.
    private static readonly string? Home = Environment.GetEnvironmentVariable("KEYKHAN_HOME");

    public static string DataDirectory { get; } =
        Home ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyKhan");

    /// <summary>Short stable id of a KEYKHAN_HOME profile; <c>null</c> for the default profile.</summary>
    public static string? ProfileKey { get; } = Home is null
        ? null
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(Home).ToUpperInvariant())))[..8];

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string DefaultVaultFile => Path.Combine(DataDirectory, "vault.keykhan");

    // Local (not roaming) on purpose: the DPAPI key only works for this account on this computer.
    public static string DeviceKeyFile { get; } = Path.Combine(
        Home ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyKhan"), "device.key");
}
