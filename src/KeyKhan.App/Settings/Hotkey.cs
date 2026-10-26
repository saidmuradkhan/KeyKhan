using System.Windows.Input;

namespace KeyKhan.App.Settings;

public readonly record struct Hotkey(ModifierKeys Modifiers, Key Key)
{
    public static Hotkey Default { get; } = new(ModifierKeys.Windows, Key.Oem3);

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
        [Key.Return] = "Enter",
    };

    public bool IsValid => Modifiers != ModifierKeys.None && Key != Key.None && !IsModifierKey(Key);

    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyNames.GetValueOrDefault(Key) ?? (Key is >= Key.D0 and <= Key.D9 ? ((int)(Key - Key.D0)).ToString() : Key.ToString()));
        return string.Join(" + ", parts);
    }
}
