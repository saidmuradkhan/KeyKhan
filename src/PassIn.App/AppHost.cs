using System.ComponentModel;
using System.Windows;
using PassIn.App.Services;
using PassIn.App.Settings;
using PassIn.App.ViewModels;
using PassIn.App.Views;

namespace PassIn.App;

/// <summary>Composition root: creates the services and windows and wires them together.</summary>
internal sealed class AppHost : IDisposable
{
    private readonly Application _app;
    private readonly SettingsStore _settings;
    private readonly VaultSession _session;
    private readonly ClipboardService _clipboard;
    private readonly HotkeyService _hotkeys;
    private readonly AutoTypeService _autoType = new();
    private readonly AutoLockService _autoLock;
    private readonly UpdateService _updates;
    private readonly MainWindow _mainWindow;
    private readonly QuickAccessWindow _quickAccess;
    private readonly TrayIcon _tray;
    private readonly DialogService _dialogs;
    private bool _exiting;

    public AppHost(Application app)
    {
        _app = app;
        _settings = new SettingsStore(AppPaths.SettingsFile);
        ApplyTheme();

        _session = new VaultSession(_settings.Current.VaultPath, AppPaths.DeviceKeyFile);
        _clipboard = new ClipboardService(_settings);
        _hotkeys = new HotkeyService();
        _autoLock = new AutoLockService(_session, _settings);
        _updates = new UpdateService(_settings);

        _mainWindow = new MainWindow();
        var dialogs = _dialogs = new DialogService(_mainWindow, _settings, _session, _clipboard, _hotkeys, _updates);
        _mainWindow.DataContext = new MainViewModel(
            _session,
            _updates,
            dialogs,
            Exit,
            () => new LockViewModel(_session),
            () => new VaultViewModel(_session, _clipboard, dialogs, _settings, RequestLock));
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.Activated += (_, _) => _updates.CheckIfDue();

        _quickAccess = new QuickAccessWindow(new QuickAccessViewModel(_session, _clipboard, _autoType, _settings), _settings);
        _quickAccess.Dismissed += (_, _) => _autoType.RestoreTarget();

        _tray = new TrayIcon(ShowMainWindow, ToggleQuickAccess, RequestLock, Exit, QuickAccessMenuText());
        _hotkeys.Pressed += (_, _) => ToggleQuickAccess();
        _session.RecoveryKeyIssued += (_, recoveryKey) => _app.Dispatcher.BeginInvoke(() =>
        {
            ShowMainWindow();
            dialogs.ShowRecoveryKey(recoveryKey);
        });
        _updates.UpdateAvailable += (_, _) => _tray.ShowInfo(
            "Update available", $"PassIn {_updates.Available!.Version.ToString(3)} is ready. Click to open PassIn and install it.");
        _settings.Changed += (_, _) => OnSettingsChanged();
    }

    public void Start(bool startHidden)
    {
        var hotkey = _settings.Current.QuickAccessHotkey;
        if (!_hotkeys.Register(hotkey))
        {
            _tray.ShowWarning("Shortcut unavailable", $"{hotkey} is used by another app. Choose a different shortcut in Settings.");
        }

        if (!startHidden || !_session.Exists)
        {
            ShowMainWindow();
        }
        _updates.StartAutomaticChecks();
    }

    public void ShowMainWindow()
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    public void Dispose()
    {
        _tray.Dispose();
        _updates.Dispose();
        _hotkeys.Dispose();
        _autoLock.Dispose();
        _clipboard.Dispose();
        _session.Dispose();
    }

    /// <summary>
    /// With "Remember me" on, a locked vault unlocks itself again at once, so Lock instead opens that setting;
    /// turning it off there locks the vault.
    /// </summary>
    private void RequestLock()
    {
        if (_session.RemembersDevice && _session.IsUnlocked)
        {
            ShowMainWindow();
            _dialogs.ShowSettings(SettingsFocus.RememberDevice);
            if (_session.RemembersDevice)
            {
                return;
            }
        }
        _session.Lock();
    }

    private void ToggleQuickAccess()
    {
        if (!_session.Exists)
        {
            ShowMainWindow();
            return;
        }
        if (_quickAccess.IsVisible)
        {
            _quickAccess.Dismiss();
            return;
        }

        _autoType.CaptureTarget();
        _quickAccess.ShowOnActiveScreen();
    }

    private void OnSettingsChanged()
    {
        ApplyTheme();
        _tray.SetQuickAccessHint(QuickAccessMenuText());
    }

    private void ApplyTheme() => _app.ThemeMode = _settings.Current.Theme switch
    {
        AppTheme.Light => ThemeMode.Light,
        AppTheme.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    private string QuickAccessMenuText() => $"Quick access\t{_settings.Current.QuickAccessHotkey}";

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        e.Cancel = true;
        if (_settings.Current.CloseToTray)
        {
            _mainWindow.Hide();
        }
        else
        {
            Exit();
        }
    }

    private void Exit()
    {
        _exiting = true;
        _app.Shutdown();
    }
}
