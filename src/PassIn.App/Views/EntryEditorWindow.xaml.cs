using System.Windows;
using PassIn.App.ViewModels;

namespace PassIn.App.Views;

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
