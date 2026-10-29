using System.Windows;
using System.Windows.Controls;
using PassIn.App.Appearance;
using PassIn.App.ViewModels;

namespace PassIn.App.Views;

public partial class AppearancePickerWindow : Window
{
    private readonly AppearancePickerViewModel _viewModel;

    public AppearancePickerWindow(AppearancePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.CloseRequested += (_, _) => DialogResult = true;
        Loaded += (_, _) =>
        {
            if (IconList.SelectedItem is not null)
            {
                IconList.ScrollIntoView(IconList.SelectedItem);
            }
        };
    }

    // Searching removes items from the list, which deselects them; only an actual pick changes the icon.
    private void OnIconSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is IconOption icon)
        {
            _viewModel.SelectedIcon = icon;
        }
    }
}
