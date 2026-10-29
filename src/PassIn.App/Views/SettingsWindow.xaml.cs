using System.Windows;
using System.Windows.Input;
using PassIn.App.ViewModels;

namespace PassIn.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.CloseRequested += (_, _) => DialogResult = true;
        Closed += (_, _) => viewModel.EndHotkeyCapture();
        if (viewModel.ExplainsRememberDevice)
        {
            Loaded += (_, _) =>
            {
                RememberDeviceOption.BringIntoView();
                RememberDeviceCheckBox.Focus();
            };
        }
    }

    private void OnHotkeyFocus(object sender, KeyboardFocusChangedEventArgs e) => _viewModel.BeginHotkeyCapture();

    private void OnHotkeyBlur(object sender, KeyboardFocusChangedEventArgs e) => _viewModel.EndHotkeyCapture();
}
