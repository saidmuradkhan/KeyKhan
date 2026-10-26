using KeyKhan.Vault.Passwords;

namespace KeyKhan.App.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed record AppSettings
{
    public KeyCombo QuickAccessHotkey { get; init; } = KeyCombo.DefaultQuickAccess;

    /// <summary>The user's shortcut choices; actions missing here use their default.</summary>
    public Dictionary<ShortcutAction, KeyCombo> Shortcuts { get; init; } = [];

    /// <summary>Lock after this many idle minutes; 0 disables idle locking.</summary>
    public int AutoLockMinutes { get; init; } = 5;

    /// <summary>Clear copied secrets after this many seconds; 0 disables clearing.</summary>
    public int ClipboardClearSeconds { get; init; } = 20;

    public bool LockOnSessionLock { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool CheckForUpdates { get; init; } = true;
    public AppTheme Theme { get; init; } = AppTheme.System;
    public string VaultPath { get; init; } = AppPaths.DefaultVaultFile;
    public PasswordGeneratorOptions Generator { get; init; } = new();

    public KeyCombo Shortcut(ShortcutAction action) =>
        Shortcuts.TryGetValue(action, out var combo) ? combo : Settings.Shortcuts.Get(action).Default;
}
