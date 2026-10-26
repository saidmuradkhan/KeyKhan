using System.Windows.Controls;
using System.Windows.Input;
using KeyKhan.App.ViewModels;

namespace KeyKhan.App.Views;

public partial class VaultView : UserControl
{
    public VaultView()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private VaultViewModel ViewModel => (VaultViewModel)DataContext;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    // Arrow keys move through results without leaving the search box; Enter copies the password.
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        int offset = e.Key switch { Key.Down => 1, Key.Up => -1, _ => 0 };
        if (offset != 0 && EntryList.Items.Count > 0)
        {
            EntryList.SelectedIndex = Math.Clamp(EntryList.SelectedIndex + offset, 0, EntryList.Items.Count - 1);
            EntryList.ScrollIntoView(EntryList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ViewModel.CopyPasswordCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e) => ViewModel.EditEntryCommand.Execute(null);
}
