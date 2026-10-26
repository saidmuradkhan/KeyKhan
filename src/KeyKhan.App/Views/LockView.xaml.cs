using System.Windows;
using System.Windows.Controls;
using KeyKhan.App.ViewModels;

namespace KeyKhan.App.Views;

public partial class LockView : UserControl
{
    public LockView()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private LockViewModel ViewModel => (LockViewModel)DataContext;

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        using var password = PasswordInput.SecurePassword;
        ViewModel.OnPasswordChanged(password);
    }

    private async void OnSubmit(object sender, RoutedEventArgs e)
    {
        // A successful unlock replaces this view, which clears its DataContext.
        var viewModel = ViewModel;
        using (var password = PasswordInput.SecurePassword)
        using (var confirmation = ConfirmInput.SecurePassword)
        {
            await viewModel.SubmitAsync(password, confirmation);
        }

        if (viewModel.Error is not null)
        {
            PasswordInput.SelectAll();
            PasswordInput.Focus();
        }
    }
}
