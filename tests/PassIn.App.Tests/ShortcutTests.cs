using System.Windows.Input;
using PassIn.App.Settings;

namespace PassIn.App.Tests;

public sealed class ShortcutTests
{
    [Theory]
    [InlineData(ModifierKeys.Control, Key.F, true)]
    [InlineData(ModifierKeys.None, Key.Delete, true)]
    [InlineData(ModifierKeys.None, Key.PageDown, true)]
    [InlineData(ModifierKeys.None, Key.F, false)]
    [InlineData(ModifierKeys.Shift, Key.A, false)]
    [InlineData(ModifierKeys.Control, Key.LeftShift, false)]
    public void Local_shortcuts_follow_gesture_rules(ModifierKeys modifiers, Key key, bool valid) =>
        Assert.Equal(valid, new KeyCombo(modifiers, key).IsValidLocal);

    [Theory]
    [InlineData(ModifierKeys.Windows, Key.K, true)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt, Key.P, true)]
    [InlineData(ModifierKeys.None, Key.F8, false)]
    public void Global_hotkeys_need_a_modifier(ModifierKeys modifiers, Key key, bool valid) =>
        Assert.Equal(valid, new KeyCombo(modifiers, key).IsValidGlobal);

    [Fact]
    public void Combos_read_naturally() =>
        Assert.Equal("Ctrl + Shift + Win + PgDn", new KeyCombo(ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows, Key.PageDown).ToString());

    [Fact]
    public void Defaults_are_valid() => Assert.Null(Shortcuts.Validate(Defaults()));

    [Fact]
    public void Duplicates_within_a_scope_are_rejected()
    {
        var shortcuts = Defaults();
        shortcuts[ShortcutAction.Lock] = shortcuts[ShortcutAction.Search];

        Assert.Contains("\"Search\"", Shortcuts.Validate(shortcuts));
    }

    [Fact]
    public void Scopes_may_reuse_a_combination()
    {
        var shortcuts = Defaults();
        shortcuts[ShortcutAction.QuickCopyPassword] = shortcuts[ShortcutAction.CopyPassword];

        Assert.Null(Shortcuts.Validate(shortcuts));
    }

    [Fact]
    public void Settings_fall_back_to_defaults() =>
        Assert.Equal(new KeyCombo(ModifierKeys.None, Key.PageDown), new AppSettings().Shortcut(ShortcutAction.QuickNext));

    private static Dictionary<ShortcutAction, KeyCombo> Defaults() => Shortcuts.All.ToDictionary(d => d.Action, d => d.Default);
}
