using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KeyKhan.App.Interop;
using KeyKhan.App.ViewModels;
using Forms = System.Windows.Forms;

namespace KeyKhan.App.Views;

public partial class QuickAccessWindow : Window
{
    private readonly QuickAccessViewModel _viewModel;

    public QuickAccessWindow(QuickAccessViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.DismissRequested += (_, _) => Dismiss();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SourceInitialized += (_, _) => Dwmapi.UseRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Raised when the user closes the window on purpose (not when it merely loses focus).</summary>
    public event EventHandler? Dismissed;

    public async void ShowOnActiveScreen()
    {
        PlaceOnScreenUnderCursor();
        Show();
        Activate();
        FocusInput();
        await _viewModel.ActivateAsync();
    }

    public void Dismiss()
    {
        if (!IsVisible)
        {
            return;
        }
        HideAndClear();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        HideAndClear();
    }

    // The window lives as long as the app; Alt+F4 only hides it. Cancel is ignored during app shutdown.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        HideAndClear();
        base.OnClosing(e);
    }

    private void HideAndClear()
    {
        Hide();
        UnlockInput.Clear();
    }

    private void PlaceOnScreenUnderCursor()
    {
        var area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = area.Left / dpi.DpiScaleX + (area.Width / dpi.DpiScaleX - Width) / 2;
        Top = area.Top / dpi.DpiScaleY + area.Height / dpi.DpiScaleY * 0.2;
    }

    private void FocusInput() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_viewModel.IsLocked)
            {
                Keyboard.Focus(UnlockInput);
            }
            else
            {
                Keyboard.Focus(QueryInput);
                QueryInput.SelectAll();
            }
        });

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickAccessViewModel.IsLocked) && IsVisible)
        {
            FocusInput();
        }
    }

    private async void OnUnlockKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        using (var password = UnlockInput.SecurePassword)
        {
            await _viewModel.UnlockAsync(password);
        }
        UnlockInput.Clear();
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) => _viewModel.CopyPasswordCommand.Execute(null);
}
