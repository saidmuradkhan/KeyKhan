using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PassIn.App.ViewModels;

namespace PassIn.App.Views;

public partial class LockView : UserControl
{
    public LockView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is LockViewModel old) old.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is LockViewModel current) current.PropertyChanged += OnViewModelPropertyChanged;
        };
    }

    private LockViewModel ViewModel => (LockViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!await ViewModel.TryUnlockWithDeviceAsync())
        {
            PasswordInput.Focus();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LockViewModel.Mode))
        {
            return;
        }
        PasswordInput.Clear();
        ConfirmInput.Clear();
        Dispatcher.BeginInvoke(() => (ViewModel.IsRecovering ? (Control)RecoveryKeyInput : PasswordInput).Focus());
    }

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
