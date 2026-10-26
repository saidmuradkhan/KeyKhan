namespace KeyKhan.App;

internal static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyKhan");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string DefaultVaultFile => Path.Combine(DataDirectory, "vault.keykhan");

    // Local (not roaming) on purpose: the DPAPI key only works for this account on this computer.
    public static string DeviceKeyFile { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyKhan", "device.key");
}
