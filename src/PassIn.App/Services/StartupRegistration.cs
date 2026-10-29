using Microsoft.Win32;

namespace PassIn.App.Services;

/// <summary>
/// "Start with Windows" lives in the registry Run key, which the installer may also set,
/// so the registry is the single source of truth.
/// </summary>
public static class StartupRegistration
{
    public const string MinimizedArgument = "--minimized";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PassIn";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {MinimizedArgument}");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Turns a Run value written under the app's former name into this app's own.</summary>
    public static void ReplaceLegacy(string legacyValueName)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            if (key?.GetValue(legacyValueName) is null)
            {
                return;
            }
            key.DeleteValue(legacyValueName);
        }
        Apply(true);
    }
}
