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
    public Hotkey QuickAccessHotkey { get; init; } = Hotkey.Default;

    /// <summary>Lock after this many idle minutes; 0 disables idle locking.</summary>
    public int AutoLockMinutes { get; init; } = 5;

    /// <summary>Clear copied secrets after this many seconds; 0 disables clearing.</summary>
    public int ClipboardClearSeconds { get; init; } = 20;

    public bool LockOnSessionLock { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public AppTheme Theme { get; init; } = AppTheme.System;
    public string VaultPath { get; init; } = AppPaths.DefaultVaultFile;
    public PasswordGeneratorOptions Generator { get; init; } = new();
}
