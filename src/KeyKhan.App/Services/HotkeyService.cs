using System.Windows.Input;
using System.Windows.Interop;
using KeyKhan.App.Interop;
using KeyKhan.App.Settings;

namespace KeyKhan.App.Services;

/// <summary>System-wide hotkey backed by a message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5A01;
    private static readonly nint MessageOnlyParent = -3;

    private readonly HwndSource _window;
    private bool _registered;

    public HotkeyService()
    {
        _window = new HwndSource(new HwndSourceParameters("KeyKhanHotkeySink") { ParentWindow = MessageOnlyParent });
        _window.AddHook(WndProc);
    }

    public event EventHandler? Pressed;

    /// <returns><c>false</c> when the combination is reserved by Windows or another application.</returns>
    public bool Register(Hotkey hotkey)
    {
        Unregister();
        if (!hotkey.IsValid)
        {
            return false;
        }

        uint modifiers = User32.ModNoRepeat;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= User32.ModAlt;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= User32.ModControl;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= User32.ModShift;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= User32.ModWin;

        _registered = User32.RegisterHotKey(_window.Handle, HotkeyId, modifiers, (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key));
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            User32.UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == User32.WmHotkey && wParam == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return 0;
    }
}
