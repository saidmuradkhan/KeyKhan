using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace PassIn.App.Settings;

/// <summary>A key with its modifiers, used for the global hotkey and for in-app shortcuts.</summary>
public readonly record struct KeyCombo(ModifierKeys Modifiers, Key Key)
{
    public static KeyCombo DefaultQuickAccess { get; } = new(ModifierKeys.Windows, Key.Oem3);

    private static readonly Dictionary<Key, string> KeyNames = new()
    {
        [Key.Oem3] = "~",
        [Key.OemMinus] = "-",
        [Key.OemPlus] = "=",
        [Key.OemComma] = ",",
        [Key.OemPeriod] = ".",
        [Key.Oem2] = "/",
        [Key.Oem1] = ";",
        [Key.Oem7] = "'",
        [Key.Oem4] = "[",
        [Key.Oem6] = "]",
        [Key.Oem5] = "\\",
        [Key.Space] = "Space",
        [Key.Enter] = "Enter",
        [Key.Escape] = "Esc",
        [Key.Delete] = "Del",
        [Key.Back] = "Backspace",
        [Key.PageUp] = "PgUp",
        [Key.PageDown] = "PgDn",
        [Key.Up] = "↑",
        [Key.Down] = "↓",
        [Key.Left] = "←",
        [Key.Right] = "→",
    };

    /// <summary>System-wide hotkeys need a modifier so they never swallow normal typing.</summary>
    [JsonIgnore]
    public bool IsValidGlobal => Modifiers != ModifierKeys.None && !IsModifierKey(Key) && Key != Key.None;

    /// <summary>In-app shortcuts follow WPF's rules: letters, digits and Shift-only combos need Ctrl, Alt or Win.</summary>
    [JsonIgnore]
    public bool IsValidLocal => TryCreateGesture(out _);

    public bool TryCreateGesture([NotNullWhen(true)] out KeyGesture? gesture)
    {
        gesture = null;
        if (Key == Key.None || IsModifierKey(Key))
        {
            return false;
        }

        try
        {
            gesture = new KeyGesture(Key, Modifiers);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

    public override string ToString()
    {
        if (Key == Key.None)
        {
            return "";
        }

        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyNames.GetValueOrDefault(Key) ?? (Key is >= Key.D0 and <= Key.D9 ? ((int)(Key - Key.D0)).ToString() : Key.ToString()));
        return string.Join(" + ", parts);
    }
}
