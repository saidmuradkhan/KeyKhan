using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Services;
using PassIn.App.ViewModels;
using PassIn.Vault;

namespace PassIn.App.Views;

public partial class VaultView : UserControl
{
    private const string EntryDragFormat = "PassIn.EntryId";

    private ShortcutBinder? _shortcuts;
    private Point _dragStart;
    private VaultEntry? _dragCandidate;

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

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        ViewModel.SelectedEntry = (VaultEntry)button.DataContext;
        OpenMenu(button, "EntryMenu");
    }

    private void OnFolderMoreClick(object sender, RoutedEventArgs e) => OpenMenu((Button)sender, "FolderMenu");

    private void OnSortClick(object sender, RoutedEventArgs e) => OpenMenu((Button)sender, "SortMenu");

    // The menus bind through the button's Tag (the view model) and DataContext (the clicked item).
    private void OpenMenu(Button button, string key)
    {
        var menu = (ContextMenu)FindResource(key);
        // A context menu inherits its font from the placement target, which here uses the icon font.
        menu.FontFamily = FontFamily;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // Drag an entry from the list onto a folder, Favorites or "New folder" in the sidebar.

    private void OnEntryMouseDown(object sender, MouseButtonEventArgs e)
    {
        bool onButton = FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null;
        _dragCandidate = onButton ? null : FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as VaultEntry;
        _dragStart = e.GetPosition(this);
    }

    private void OnEntryMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var offset = e.GetPosition(this) - _dragStart;
        if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var entry = _dragCandidate;
        _dragCandidate = null;
        DragDrop.DoDragDrop(EntryList, new DataObject(EntryDragFormat, entry.Id.ToString()), DragDropEffects.Move);
    }

    private void OnNavigationDragOver(object sender, DragEventArgs e)
    {
        var item = (ListBoxItem)sender;
        bool accepts = DraggedEntry(e) is not null && item.DataContext is NavigationItem target && VaultViewModel.AcceptsDrop(target);
        ShowDropTarget(item, e, accepts);
    }

    private void OnNavigationDrop(object sender, DragEventArgs e)
    {
        var item = (ListBoxItem)sender;
        HideDropTarget(item);
        if (DraggedEntry(e) is { } id && item.DataContext is NavigationItem target && VaultViewModel.AcceptsDrop(target))
        {
            ViewModel.DropEntry(id, target);
        }
    }

    private void OnNewFolderDragOver(object sender, DragEventArgs e) =>
        ShowDropTarget((Control)sender, e, DraggedEntry(e) is not null);

    private void OnNewFolderDrop(object sender, DragEventArgs e)
    {
        HideDropTarget((Control)sender);
        if (DraggedEntry(e) is { } id)
        {
            ViewModel.DropEntryOnNewFolder(id);
        }
    }

    private void OnDropTargetLeave(object sender, DragEventArgs e) => HideDropTarget((Control)sender);

    private void ShowDropTarget(Control target, DragEventArgs e, bool accepts)
    {
        e.Effects = accepts ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        if (accepts)
        {
            target.SetResourceReference(BackgroundProperty, "AccentFillColorSecondaryBrush");
        }
    }

    private static void HideDropTarget(Control target) => target.ClearValue(BackgroundProperty);

    private static Guid? DraggedEntry(DragEventArgs e) =>
        e.Data.GetData(EntryDragFormat) is string id && Guid.TryParse(id, out var guid) ? guid : null;

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null and not T)
        {
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return element as T;
    }
}
