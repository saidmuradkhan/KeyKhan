using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyKhan.App.Settings;

namespace KeyKhan.App.Controls;

/// <summary>Read-only text box that records the next key combination pressed while it has focus.</summary>
public sealed class KeyComboBox : TextBox
{
    public static readonly DependencyProperty ComboProperty = DependencyProperty.Register(
        nameof(Combo), typeof(KeyCombo), typeof(KeyComboBox),
        new FrameworkPropertyMetadata(default(KeyCombo), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((KeyComboBox)d).Text = e.NewValue.ToString()));

    public KeyComboBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        ContextMenu = null;
        TextAlignment = TextAlignment.Center;
    }

    public KeyCombo Combo
    {
        get => (KeyCombo)GetValue(ComboProperty);
        set => SetValue(ComboProperty, value);
    }

    /// <summary>Global hotkeys need a modifier; in-app shortcuts may also be keys like Del or PgDn.</summary>
    public bool IsGlobal { get; set; }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Tab)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        var candidate = new KeyCombo(Keyboard.Modifiers, key);
        if (IsGlobal ? candidate.IsValidGlobal : candidate.IsValidLocal)
        {
            Combo = candidate;
        }
    }
}
