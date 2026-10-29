using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PassIn.App.Interop;

/// <summary>
/// System-wide WH_KEYBOARD_LL hook. The callback sees physical key-downs before the shell does
/// and can swallow them.
/// </summary>
internal sealed partial class LowLevelKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    private delegate nint HookProc(int code, nint message, nint data);

    private readonly HookProc _proc;
    private readonly Func<int, bool> _onKeyDown;
    private readonly nint _handle;

    /// <param name="onKeyDown">Receives the virtual-key code; returns <c>true</c> to swallow the key.</param>
    public LowLevelKeyboardHook(Func<int, bool> onKeyDown)
    {
        _onKeyDown = onKeyDown;
        _proc = Callback; // kept in a field so the delegate outlives the native hook
        _handle = SetWindowsHookExW(WhKeyboardLl, Marshal.GetFunctionPointerForDelegate(_proc), GetModuleHandleW(null), 0);
        if (_handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    public void Dispose() => UnhookWindowsHookEx(_handle);

    private unsafe nint Callback(int code, nint message, nint data)
    {
        if (code >= 0 && message is WmKeyDown or WmSysKeyDown)
        {
            if (_onKeyDown((int)((KeyboardHookData*)data)->VirtualKey))
            {
                return 1;
            }
        }
        return CallNextHookEx(_handle, code, message, data);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowsHookExW(int hookType, nint proc, nint module, uint threadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string? moduleName);
}
