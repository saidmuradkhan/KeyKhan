using System.Windows;
using PassIn.App.ViewModels;

namespace PassIn.App.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.Completed += (_, _) => DialogResult = true;
        Loaded += (_, _) => CurrentInput.Focus();
    }

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        using var password = NewInput.SecurePassword;
        _viewModel.OnNewPasswordChanged(password);
    }

    private async void OnSubmit(object sender, RoutedEventArgs e)
    {
        using var current = CurrentInput.SecurePassword;
        using var next = NewInput.SecurePassword;
        using var confirmation = ConfirmInput.SecurePassword;
        await _viewModel.SubmitAsync(current, next, confirmation);
    }
}
