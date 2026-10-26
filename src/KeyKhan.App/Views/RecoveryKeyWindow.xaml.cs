using System.ComponentModel;
using System.Windows;
using KeyKhan.App.ViewModels;

namespace KeyKhan.App.Views;

public partial class RecoveryKeyWindow : Window
{
    private readonly RecoveryKeyViewModel _viewModel;

    public RecoveryKeyWindow(RecoveryKeyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.CloseRequested += (_, _) => DialogResult = true;
    }

    // The key is never shown again, so the window stays until the user confirms it was saved.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = !_viewModel.IsSaved;
        base.OnClosing(e);
    }
}
