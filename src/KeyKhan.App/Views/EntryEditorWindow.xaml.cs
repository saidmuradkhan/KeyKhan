using System.Windows;
using KeyKhan.App.ViewModels;

namespace KeyKhan.App.Views;

public partial class EntryEditorWindow : Window
{
    public EntryEditorWindow(EntryEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => DialogResult = true;
        Loaded += (_, _) => TitleInput.Focus();
    }
}
