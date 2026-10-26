using System.ComponentModel;
using System.Windows;
using KeyKhan.App.Services;
using KeyKhan.App.Settings;
using KeyKhan.App.ViewModels;
using KeyKhan.App.Views;

namespace KeyKhan.App;

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
    private readonly MainWindow _mainWindow;
    private readonly QuickAccessWindow _quickAccess;
    private readonly TrayIcon _tray;
    private bool _exiting;

    public AppHost(Application app)
    {
        _app = app;
        _settings = new SettingsStore(AppPaths.SettingsFile);
        ApplyTheme();

        _session = new VaultSession(_settings.Current.VaultPath);
        _clipboard = new ClipboardService(_settings);
        _hotkeys = new HotkeyService();
        _autoLock = new AutoLockService(_session, _settings);

        _mainWindow = new MainWindow();
        var dialogs = new DialogService(_mainWindow, _settings, _session, _clipboard, _hotkeys);
        _mainWindow.DataContext = new MainViewModel(
            _session,
            () => new LockViewModel(_session),
            () => new VaultViewModel(_session, _clipboard, dialogs));
        _mainWindow.Closing += OnMainWindowClosing;

        _quickAccess = new QuickAccessWindow(new QuickAccessViewModel(_session, _clipboard, _autoType));
        _quickAccess.Dismissed += (_, _) => _autoType.RestoreTarget();

        _tray = new TrayIcon(ShowMainWindow, ToggleQuickAccess, _session.Lock, Exit, QuickAccessMenuText());
        _hotkeys.Pressed += (_, _) => ToggleQuickAccess();
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
        _hotkeys.Dispose();
        _autoLock.Dispose();
        _clipboard.Dispose();
        _session.Dispose();
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
        StartupRegistration.Apply(_settings.Current.StartWithWindows);
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
