using System.Security.Cryptography;
using System.Text;

namespace PassIn.App;

internal static class AppPaths
{
    // PASSIN_HOME keeps everything in one folder instead, e.g. for a second profile or for development.
    private static readonly string? Home = Environment.GetEnvironmentVariable("PASSIN_HOME");

    public static string DataDirectory { get; } =
        Home ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PassIn");

    public static bool IsDefaultProfile => Home is null;

    /// <summary>Short stable id of a PASSIN_HOME profile; <c>null</c> for the default profile.</summary>
    public static string? ProfileKey { get; } = Home is null
        ? null
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(Home).ToUpperInvariant())))[..8];

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string DefaultVaultFile => Path.Combine(DataDirectory, "vault.passin");

    // Local (not roaming) on purpose: the DPAPI key only works for this account on this computer.
    public static string DeviceKeyFile { get; } = Path.Combine(
        Home ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PassIn"), "device.key");
}
