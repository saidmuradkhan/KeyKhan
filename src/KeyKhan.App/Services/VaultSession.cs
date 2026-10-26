using System.Security;
using KeyKhan.Vault;

namespace KeyKhan.App.Services;

/// <summary>Owns the currently unlocked vault and notifies the UI about lock state and data changes.</summary>
public sealed class VaultSession(string path, string deviceKeyPath) : IDisposable
{
    public const int MinimumMasterPasswordLength = 8;

    private PasswordVault? _vault;

    public string Path { get; } = path;
    public bool Exists => File.Exists(Path);
    public bool IsUnlocked => _vault is not null;
    public IReadOnlyList<VaultEntry> Entries => _vault?.Entries ?? [];

    /// <summary>Whether this Windows account can unlock the vault without the master password.</summary>
    public bool RemembersDevice => File.Exists(deviceKeyPath);

    public IReadOnlyList<string> Folders => _vault?.Folders ?? [];

    public event EventHandler? StateChanged;
    public event EventHandler? EntriesChanged;

    /// <summary>A new vault, or a 1.0 vault upgraded on unlock, comes with a recovery key the user must save.</summary>
    public event EventHandler<string>? RecoveryKeyIssued;

    public async Task CreateAsync(SecureString masterPassword) =>
        SetVault(await Task.Run(() => PasswordVault.Create(Path, masterPassword, out _)));

    /// <exception cref="VaultAuthenticationException">Wrong master password.</exception>
    public async Task UnlockAsync(SecureString masterPassword) =>
        SetVault(await Task.Run(() => PasswordVault.Open(Path, masterPassword)));

    /// <exception cref="VaultAuthenticationException">Wrong recovery key.</exception>
    public async Task RecoverAsync(string recoveryKey, SecureString newMasterPassword) =>
        SetVault(await Task.Run(() => PasswordVault.Recover(Path, recoveryKey, newMasterPassword)));

    /// <summary>Unlocks with the key remembered for this Windows account, if there is a valid one.</summary>
    public async Task<bool> TryUnlockWithDeviceAsync()
    {
        if (IsUnlocked || !RemembersDevice || !Exists)
        {
            return IsUnlocked;
        }

        try
        {
            byte[] deviceKey = await File.ReadAllBytesAsync(deviceKeyPath);
            SetVault(await Task.Run(() => PasswordVault.OpenWithDeviceKey(Path, deviceKey)));
            return true;
        }
        catch (VaultAuthenticationException)
        {
            // Another vault, another account, or a changed file: fall back to the master password.
            ForgetDevice();
            return false;
        }
    }

    public void RememberDevice()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(deviceKeyPath)!);
        File.WriteAllBytes(deviceKeyPath, RequireVault().CreateDeviceKey());
    }

    public void ForgetDevice() => File.Delete(deviceKeyPath);

    public string RegenerateRecoveryKey() => RequireVault().RegenerateRecoveryKey();

    public void Lock()
    {
        if (_vault is null)
        {
            return;
        }
        _vault.Dispose();
        _vault = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public VaultEntry? Find(Guid id) => _vault?.Find(id);

    public void Save(VaultEntry entry) => Mutate(v => v.Upsert(entry));

    public void Delete(Guid id) => Mutate(v => v.Remove(id));

    public void Import(IEnumerable<VaultEntry> entries) => Mutate(v => v.AddRange(entries));

    public void AddFolder(string name) => Mutate(v => v.AddFolder(name));

    public void RenameFolder(string name, string newName) => Mutate(v => v.RenameFolder(name, newName));

    public void DeleteFolder(string name) => Mutate(v => v.DeleteFolder(name));

    public Task ChangeMasterPasswordAsync(SecureString current, SecureString next)
    {
        var vault = RequireVault();
        return Task.Run(() => vault.ChangeMasterPassword(current, next));
    }

    public void Dispose() => _vault?.Dispose();

    private void SetVault(PasswordVault vault)
    {
        _vault?.Dispose();
        _vault = vault;
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (vault.NewRecoveryKey is { } recoveryKey)
        {
            RecoveryKeyIssued?.Invoke(this, recoveryKey);
        }
    }

    private void Mutate(Action<PasswordVault> change)
    {
        change(RequireVault());
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    private PasswordVault RequireVault() =>
        _vault ?? throw new InvalidOperationException("The vault is locked.");
}
