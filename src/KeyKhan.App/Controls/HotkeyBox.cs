using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyKhan.App.Settings;

namespace KeyKhan.App.Controls;

/// <summary>Read-only text box that records the next modifier + key combination pressed while focused.</summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(Hotkey), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(default(Hotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((HotkeyBox)d).Text = e.NewValue.ToString()));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        ContextMenu = null;
    }

    public Hotkey Hotkey
    {
        get => (Hotkey)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None && key is Key.Tab or Key.Escape)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        var candidate = new Hotkey(modifiers, key);
        if (candidate.IsValid)
        {
            Hotkey = candidate;
        }
    }
}
