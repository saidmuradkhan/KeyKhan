using System.Security;
using KeyKhan.Vault;

namespace KeyKhan.App.Services;

/// <summary>Owns the currently unlocked vault and notifies the UI about lock state and data changes.</summary>
public sealed class VaultSession(string path) : IDisposable
{
    public const int MinimumMasterPasswordLength = 8;

    private PasswordVault? _vault;

    public string Path { get; } = path;
    public bool Exists => File.Exists(Path);
    public bool IsUnlocked => _vault is not null;
    public IReadOnlyList<VaultEntry> Entries => _vault?.Entries ?? [];

    public IReadOnlyList<string> Folders => Entries
        .Select(e => e.Folder)
        .Where(f => f.Length > 0)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public event EventHandler? StateChanged;
    public event EventHandler? EntriesChanged;

    public async Task CreateAsync(SecureString masterPassword) =>
        SetVault(await Task.Run(() => PasswordVault.Create(Path, masterPassword)));

    /// <exception cref="VaultAuthenticationException">Wrong master password.</exception>
    public async Task UnlockAsync(SecureString masterPassword) =>
        SetVault(await Task.Run(() => PasswordVault.Open(Path, masterPassword)));

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
    }

    private void Mutate(Action<PasswordVault> change)
    {
        change(RequireVault());
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    private PasswordVault RequireVault() =>
        _vault ?? throw new InvalidOperationException("The vault is locked.");
}
