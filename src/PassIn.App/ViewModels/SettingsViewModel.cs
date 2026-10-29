using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Services;
using PassIn.App.Settings;
using PassIn.Vault.Interchange;

namespace PassIn.App.ViewModels;

public sealed record Choice<T>(T Value, string Label);

public enum SettingsFocus
{
    None,

    /// <summary>Opened from Lock while "Remember me" keeps the vault from staying locked.</summary>
    RememberDevice,
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string CsvFilter = "CSV files (*.csv)|*.csv";

    private readonly SettingsStore _settings;
    private readonly HotkeyService _hotkeys;
    private readonly VaultSession _session;
    private readonly UpdateService _updates;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private KeyCombo _quickAccessHotkey;

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
    private bool _checkForUpdates;

    [ObservableProperty]
    private string? _updateStatus;

    [ObservableProperty]
    private string? _error;

    public SettingsViewModel(
        SettingsStore settings, HotkeyService hotkeys, VaultSession session, UpdateService updates, IDialogService dialogs,
        SettingsFocus focus = SettingsFocus.None)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _session = session;
        _updates = updates;
        Focus = focus;
        _dialogs = dialogs;

        var current = settings.Current;
        _quickAccessHotkey = current.QuickAccessHotkey;
        MainShortcuts = ShortcutItems(ShortcutScope.MainWindow, current);
        QuickAccessShortcuts = ShortcutItems(ShortcutScope.QuickAccess, current);
        _autoLockMinutes = current.AutoLockMinutes;
        _clipboardClearSeconds = current.ClipboardClearSeconds;
        _lockOnSessionLock = current.LockOnSessionLock;
        _rememberDevice = session.RemembersDevice;
        _startWithWindows = StartupRegistration.IsEnabled;
        _closeToTray = current.CloseToTray;
        _theme = current.Theme;
        _checkForUpdates = current.CheckForUpdates;
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

    public IReadOnlyList<ShortcutItemViewModel> MainShortcuts { get; }
    public IReadOnlyList<ShortcutItemViewModel> QuickAccessShortcuts { get; }

    public SettingsFocus Focus { get; }
    public bool ExplainsRememberDevice => Focus == SettingsFocus.RememberDevice;

    public string VersionText => $"PassIn {UpdateService.CurrentVersion.ToString(3)}";

    public string VaultPath => _session.Path;
    public bool CanManageVault => _session.IsUnlocked;

    /// <summary>
    /// While the hotkey box has focus every combination goes to it, including ones Windows would act on,
    /// and the current hotkey is paused so it does not fire.
    /// </summary>
    public void BeginHotkeyCapture() => _hotkeys.BeginCapture(combo => QuickAccessHotkey = combo);

    public void EndHotkeyCapture()
    {
        _hotkeys.EndCapture();
        _hotkeys.Register(_settings.Current.QuickAccessHotkey);
    }

    partial void OnQuickAccessHotkeyChanged(KeyCombo value) => Error = null;

    [RelayCommand]
    private void ResetHotkey() => QuickAccessHotkey = KeyCombo.DefaultQuickAccess;

    [RelayCommand]
    private void ResetShortcuts()
    {
        foreach (var item in MainShortcuts.Concat(QuickAccessShortcuts))
        {
            item.ResetCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void Save()
    {
        var shortcuts = MainShortcuts.Concat(QuickAccessShortcuts).ToDictionary(i => i.Definition.Action, i => i.Combo);
        Error = Shortcuts.Validate(shortcuts);
        if (Error is not null)
        {
            return;
        }

        if (!_hotkeys.Register(QuickAccessHotkey))
        {
            Error = QuickAccessHotkey == new KeyCombo(System.Windows.Input.ModifierKeys.Windows, System.Windows.Input.Key.L)
                ? "Win + L always locks Windows and cannot be used. Choose a different shortcut."
                : $"{QuickAccessHotkey} is already used by another app. Choose a different shortcut.";
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
            QuickAccessHotkey = QuickAccessHotkey,
            Shortcuts = shortcuts,
            AutoLockMinutes = AutoLockMinutes,
            ClipboardClearSeconds = ClipboardClearSeconds,
            LockOnSessionLock = LockOnSessionLock,
            CloseToTray = CloseToTray,
            Theme = Theme,
            CheckForUpdates = CheckForUpdates,
        });
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static List<ShortcutItemViewModel> ShortcutItems(ShortcutScope scope, AppSettings current) =>
        Shortcuts.All.Where(d => d.Scope == scope).Select(d => new ShortcutItemViewModel(d, current.Shortcut(d.Action))).ToList();

    [RelayCommand]
    private async Task CheckForUpdatesNow()
    {
        UpdateStatus = "Checking…";
        try
        {
            var release = await _updates.CheckAsync();
            UpdateStatus = release is null
                ? "You have the latest version."
                : $"PassIn {release.Version.ToString(3)} is available — use the banner in the main window to install it.";
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            UpdateStatus = "Could not reach GitHub. Check your internet connection.";
        }
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
        if (_dialogs.PickFileToSave(CsvFilter, "passin-export.csv") is not { } path)
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
