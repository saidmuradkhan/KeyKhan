using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KeyKhan.App.Services;
using KeyKhan.App.ViewModels;

namespace KeyKhan.App.Views;

public partial class VaultView : UserControl
{
    private ShortcutBinder? _shortcuts;

    public VaultView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _shortcuts?.Dispose();
    }

    private VaultViewModel ViewModel => (VaultViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _shortcuts?.Dispose();
        _shortcuts = new ShortcutBinder(this, ViewModel.Settings, ViewModel.ShortcutCommands(new RelayCommand(FocusSearch)));
        SearchBox.Focus();
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
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
