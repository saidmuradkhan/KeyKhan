using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PassIn.App.Interop;
using PassIn.App.Settings;

namespace PassIn.App.Services;

/// <summary>
/// The system-wide quick-access hotkey. Uses RegisterHotKey where possible; Windows reserves most
/// Win + key combinations for itself, so those are caught with a low-level keyboard hook instead.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5A01;
    private const ushort VkUnassigned = 0xE8;
    private static readonly nint MessageOnlyParent = -3;

    private readonly HwndSource _window;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private LowLevelKeyboardHook? _hook;
    private KeyCombo? _hooked;
    private Action<KeyCombo>? _capture;
    private bool _registered;

    public HotkeyService()
    {
        _window = new HwndSource(new HwndSourceParameters("PassInHotkeySink") { ParentWindow = MessageOnlyParent });
        _window.AddHook(WndProc);
    }

    public event EventHandler? Pressed;

    /// <returns><c>false</c> when another application owns the combination, or for Win + L, which Windows never gives up.</returns>
    public bool Register(KeyCombo combo)
    {
        Unregister();
        if (!combo.IsValidGlobal || combo == new KeyCombo(ModifierKeys.Windows, Key.L))
        {
            return false;
        }

        _registered = User32.RegisterHotKey(_window.Handle, HotkeyId, NativeModifiers(combo.Modifiers),
            (uint)KeyInterop.VirtualKeyFromKey(combo.Key));
        if (_registered)
        {
            return true;
        }
        if (!combo.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            return false;
        }

        _hooked = combo;
        UpdateHook();
        return true;
    }

    public void Unregister()
    {
        if (_registered)
        {
            User32.UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }
        _hooked = null;
        UpdateHook();
    }

    /// <summary>
    /// Records the next key combination, including ones the shell would normally act on (Win + E, Win + V, …).
    /// </summary>
    public void BeginCapture(Action<KeyCombo> onCaptured)
    {
        Unregister();
        _capture = onCaptured;
        UpdateHook();
    }

    public void EndCapture()
    {
        _capture = null;
        UpdateHook();
    }

    public void Dispose()
    {
        Unregister();
        _capture = null;
        UpdateHook();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }

    // The hook sees every keystroke, so it only exists while it is actually needed.
    private void UpdateHook()
    {
        bool needed = _hooked is not null || _capture is not null;
        if (needed && _hook is null)
        {
            _hook = new LowLevelKeyboardHook(OnKeyDown);
        }
        else if (!needed && _hook is not null)
        {
            _hook.Dispose();
            _hook = null;
        }
    }

    private bool OnKeyDown(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        if (KeyCombo.IsModifierKey(key))
        {
            return false;
        }

        var combo = new KeyCombo(PressedModifiers(), key);
        if (_capture is { } capture && combo.IsValidGlobal)
        {
            _dispatcher.BeginInvoke(() => capture(combo));
            return Swallow(combo);
        }
        if (_hooked == combo)
        {
            _dispatcher.BeginInvoke(() => Pressed?.Invoke(this, EventArgs.Empty));
            return Swallow(combo);
        }
        return false;
    }

    // Releasing Win after a swallowed key would open the Start menu; an unassigned key in between prevents that.
    private static bool Swallow(KeyCombo combo)
    {
        if (combo.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            User32.Input[] inputs =
            [
                KeyInput(VkUnassigned, 0),
                KeyInput(VkUnassigned, User32.KeyEventKeyUp),
            ];
            User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.Input>());
        }
        return true;
    }

    private static User32.Input KeyInput(ushort virtualKey, uint flags) => new()
    {
        Type = User32.InputKeyboard,
        Data = new User32.InputUnion { Keyboard = new User32.KeyboardInput { VirtualKey = virtualKey, Flags = flags } },
    };

    private static ModifierKeys PressedModifiers()
    {
        static bool Down(int vk) => (User32.GetAsyncKeyState(vk) & 0x8000) != 0;

        var modifiers = ModifierKeys.None;
        if (Down(0x11)) modifiers |= ModifierKeys.Control;
        if (Down(0x12)) modifiers |= ModifierKeys.Alt;
        if (Down(0x10)) modifiers |= ModifierKeys.Shift;
        if (Down(0x5B) || Down(0x5C)) modifiers |= ModifierKeys.Windows;
        return modifiers;
    }

    private static uint NativeModifiers(ModifierKeys modifiers)
    {
        uint native = User32.ModNoRepeat;
        if (modifiers.HasFlag(ModifierKeys.Alt)) native |= User32.ModAlt;
        if (modifiers.HasFlag(ModifierKeys.Control)) native |= User32.ModControl;
        if (modifiers.HasFlag(ModifierKeys.Shift)) native |= User32.ModShift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) native |= User32.ModWin;
        return native;
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
