using System.Runtime.InteropServices;

namespace PassIn.App.Interop;

internal static partial class Dwmapi
{
    private const int WindowCornerPreference = 33;
    private const int CornerRound = 2;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>Windows 11 does not round borderless windows by default. No effect on Windows 10.</summary>
    public static void UseRoundedCorners(nint hwnd)
    {
        int preference = CornerRound;
        DwmSetWindowAttribute(hwnd, WindowCornerPreference, ref preference, sizeof(int));
    }
}
