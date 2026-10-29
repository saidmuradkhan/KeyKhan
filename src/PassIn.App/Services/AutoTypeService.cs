using System.Runtime.InteropServices;
using PassIn.App.Interop;

namespace PassIn.App.Services;

/// <summary>
/// Remembers the window that was active before PassIn took focus, and types into it via SendInput.
/// </summary>
public sealed class AutoTypeService
{
    private const ushort VkTab = 0x09;
    private const ushort VkReturn = 0x0D;
    private static readonly int[] ModifierKeys = [0x10, 0x11, 0x12, 0x5B, 0x5C]; // Shift, Ctrl, Alt, LWin, RWin

    private nint _target;

    public void CaptureTarget() => _target = User32.GetForegroundWindow();

    public void RestoreTarget()
    {
        if (_target != 0)
        {
            User32.SetForegroundWindow(_target);
        }
    }

    /// <summary>Types <c>username [Tab] password [Enter]</c> into the remembered window.</summary>
    public async Task TypeLoginAsync(string username, string password)
    {
        var inputs = new List<User32.Input>();
        if (username.Length > 0)
        {
            AddText(inputs, username);
            AddKey(inputs, VkTab);
        }
        AddText(inputs, password);
        AddKey(inputs, VkReturn);

        // Keys still held from the shortcut would otherwise combine with the typed characters.
        await WaitForModifiersReleasedAsync();
        RestoreTarget();
        await Task.Delay(100);

        User32.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<User32.Input>());
    }

    private static async Task WaitForModifiersReleasedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (ModifierKeys.Any(vk => (User32.GetAsyncKeyState(vk) & 0x8000) != 0) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private static void AddText(List<User32.Input> inputs, string text)
    {
        foreach (char c in text)
        {
            inputs.Add(Keyboard(0, c, User32.KeyEventUnicode));
            inputs.Add(Keyboard(0, c, User32.KeyEventUnicode | User32.KeyEventKeyUp));
        }
    }

    private static void AddKey(List<User32.Input> inputs, ushort virtualKey)
    {
        inputs.Add(Keyboard(virtualKey, 0, 0));
        inputs.Add(Keyboard(virtualKey, 0, User32.KeyEventKeyUp));
    }

    private static User32.Input Keyboard(ushort virtualKey, ushort scanCode, uint flags) => new()
    {
        Type = User32.InputKeyboard,
        Data = new User32.InputUnion
        {
            Keyboard = new User32.KeyboardInput { VirtualKey = virtualKey, ScanCode = scanCode, Flags = flags },
        },
    };
}
