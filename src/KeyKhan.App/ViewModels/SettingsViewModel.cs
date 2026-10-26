using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKhan.App.Services;
using KeyKhan.App.Settings;
using KeyKhan.Vault.Interchange;

namespace KeyKhan.App.ViewModels;

public sealed record Choice<T>(T Value, string Label);

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string CsvFilter = "CSV files (*.csv)|*.csv";

    private readonly SettingsStore _settings;
    private readonly HotkeyService _hotkeys;
    private readonly VaultSession _session;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private Hotkey _hotkey;

    [ObservableProperty]
    private int _autoLockMinutes;

    [ObservableProperty]
    private int _clipboardClearSeconds;

    [ObservableProperty]
    private bool _lockOnSessionLock;

    [ObservableProperty]
    private bool _rememberDevice;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private string? _error;

    public SettingsViewModel(SettingsStore settings, HotkeyService hotkeys, VaultSession session, IDialogService dialogs)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _session = session;
        _dialogs = dialogs;

        var current = settings.Current;
        _hotkey = current.QuickAccessHotkey;
        _autoLockMinutes = current.AutoLockMinutes;
        _clipboardClearSeconds = current.ClipboardClearSeconds;
        _lockOnSessionLock = current.LockOnSessionLock;
        _rememberDevice = session.RemembersDevice;
        _startWithWindows = StartupRegistration.IsEnabled;
        _closeToTray = current.CloseToTray;
        _theme = current.Theme;
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<Choice<int>> AutoLockChoices { get; } =
    [
        new(1, "After 1 minute"), new(5, "After 5 minutes"), new(15, "After 15 minutes"),
        new(30, "After 30 minutes"), new(60, "After 1 hour"), new(0, "Never"),
    ];

    public IReadOnlyList<Choice<int>> ClipboardChoices { get; } =
    [
        new(10, "After 10 seconds"), new(20, "After 20 seconds"), new(30, "After 30 seconds"),
        new(60, "After 1 minute"), new(0, "Never"),
    ];

    public IReadOnlyList<Choice<AppTheme>> ThemeChoices { get; } =
    [
        new(AppTheme.System, "Use Windows setting"), new(AppTheme.Light, "Light"), new(AppTheme.Dark, "Dark"),
    ];

    public string VaultPath => _session.Path;
    public bool CanManageVault => _session.IsUnlocked;

    /// <summary>The global hotkey must not fire while the user is recording a new one.</summary>
    public void BeginHotkeyCapture() => _hotkeys.Unregister();

    public void EndHotkeyCapture() => _hotkeys.Register(_settings.Current.QuickAccessHotkey);

    partial void OnHotkeyChanged(Hotkey value) => Error = null;

    [RelayCommand]
    private void ResetHotkey() => Hotkey = Hotkey.Default;

    [RelayCommand]
    private void Save()
    {
        if (!_hotkeys.Register(Hotkey))
        {
            Error = $"{Hotkey} is already used by Windows or another app. Choose a different shortcut.";
            _hotkeys.Register(_settings.Current.QuickAccessHotkey);
            return;
        }

        if (StartWithWindows != StartupRegistration.IsEnabled)
        {
            StartupRegistration.Apply(StartWithWindows);
        }

        if (RememberDevice != _session.RemembersDevice && _session.IsUnlocked)
        {
            if (RememberDevice)
            {
                _session.RememberDevice();
            }
            else
            {
                _session.ForgetDevice();
            }
        }

        _settings.Update(s => s with
        {
            QuickAccessHotkey = Hotkey,
            AutoLockMinutes = AutoLockMinutes,
            ClipboardClearSeconds = ClipboardClearSeconds,
            LockOnSessionLock = LockOnSessionLock,
            CloseToTray = CloseToTray,
            Theme = Theme,
        });
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ShowVaultFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_session.Path}\"") { UseShellExecute = true });

    [RelayCommand(CanExecute = nameof(CanManageVault))]
    private void ChangeMasterPassword() => _dialogs.ShowChangeMasterPassword();

    [RelayCommand(CanExecute = nameof(CanManageVault))]
    private void NewRecoveryKey()
    {
        if (_dialogs.Confirm("New recovery key", "Create a new recovery key? Your current recovery key will stop working."))
        {
            _dialogs.ShowRecoveryKey(_session.RegenerateRecoveryKey());
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageVault))]
    private void Import()
    {
        if (_dialogs.PickFileToOpen(CsvFilter) is not { } path)
        {
            return;
        }

        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var entries = CsvInterchange.Import(reader);
            _session.Import(entries);
            _dialogs.ShowMessage("Import complete", $"Imported {entries.Count} entries.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _dialogs.ShowMessage("Import failed", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageVault))]
    private void Export()
    {
        if (!_dialogs.Confirm("Export passwords",
                "The CSV file will contain all your passwords in plain text. Anyone with access to the file can read them. Continue?"))
        {
            return;
        }
        if (_dialogs.PickFileToSave(CsvFilter, "keykhan-export.csv") is not { } path)
        {
            return;
        }

        try
        {
            using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            CsvInterchange.Export(_session.Entries, writer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowMessage("Export failed", ex.Message);
        }
    }
}
