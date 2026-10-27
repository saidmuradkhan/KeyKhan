using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using KeyKhan.Vault.Security;
using KeyKhan.Vault.Storage;

namespace KeyKhan.Vault;

/// <summary>
/// An unlocked vault. Entries are encrypted with a random data key, which is stored wrapped
/// under the master password and under a recovery key. Every mutation is persisted immediately.
/// </summary>
public sealed class PasswordVault : IDisposable
{
    private readonly string _path;
    private static readonly StringComparer FolderComparer = StringComparer.OrdinalIgnoreCase;

    private readonly List<VaultEntry> _entries;
    private readonly List<string> _folders;
    private readonly Dictionary<string, FolderStyle> _folderStyles;
    private readonly SecretKey _dataKey;
    private VaultHeader _header;

    private PasswordVault(string path, VaultHeader header, SecretKey dataKey, VaultPayload payload)
    {
        _path = path;
        _header = header;
        _dataKey = dataKey;
        _entries = payload.Entries;
        _folders = payload.Folders ?? [];
        _folderStyles = new Dictionary<string, FolderStyle>(payload.FolderStyles ?? [], FolderComparer);
    }

    public IReadOnlyList<VaultEntry> Entries => _entries;

    /// <summary>Every folder in use or created by the user, sorted by name.</summary>
    public IReadOnlyList<string> Folders => _folders
        .Concat(_entries.Select(e => e.Folder))
        .Where(f => f.Length > 0)
        .Distinct(FolderComparer)
        .Order(FolderComparer)
        .ToList();

    /// <param name="recoveryKey">Shown to the user once; the only way back in without the master password.</param>
    public static PasswordVault Create(string path, SecureString masterPassword, out string recoveryKey) =>
        Create(path, masterPassword, KeySlot.PasswordIterations, out recoveryKey);

    internal static PasswordVault Create(string path, SecureString masterPassword, int passwordIterations, out string recoveryKey)
    {
        if (File.Exists(path))
        {
            throw new IOException($"A vault already exists at '{path}'.");
        }

        var vault = Initialize(path, new VaultPayload([]), masterPassword, passwordIterations);
        recoveryKey = vault.NewRecoveryKey!;
        return vault;
    }

    /// <summary>
    /// Opens the vault. A vault from KeyKhan 1.0 is upgraded to the current format on the way: the original is kept
    /// next to it as <c>.v1.bak</c> and a recovery key is issued in <see cref="NewRecoveryKey"/>.
    /// </summary>
    /// <exception cref="VaultAuthenticationException">Wrong master password or damaged file.</exception>
    public static PasswordVault Open(string path, SecureString masterPassword)
    {
        byte[] content = File.ReadAllBytes(path);
        if (!LegacyFormat.IsLegacy(content))
        {
            return Open(path, header => masterPassword.Use(header.Slot(KeySlotKind.Password).Unlock));
        }

        var payload = LegacyFormat.Decrypt(content, masterPassword);
        File.Copy(path, path + ".v1.bak", overwrite: true);
        return Initialize(path, payload, masterPassword, KeySlot.PasswordIterations);
    }

    /// <summary>
    /// A recovery key created while opening this vault (new vault or upgraded 1.0 vault). Show it to the user once.
    /// </summary>
    public string? NewRecoveryKey { get; private init; }

    /// <summary>Opens the vault with its recovery key and replaces the forgotten master password.</summary>
    /// <exception cref="VaultAuthenticationException">The recovery key is wrong.</exception>
    public static PasswordVault Recover(string path, string recoveryKey, SecureString newMasterPassword)
    {
        string symbols = RecoveryKey.Normalize(recoveryKey) ?? throw new VaultAuthenticationException();
        var vault = Open(path, header => header.Slot(KeySlotKind.RecoveryKey).Unlock(symbols));
        try
        {
            vault.SetMasterPassword(newMasterPassword);
            return vault;
        }
        catch
        {
            vault.Dispose();
            throw;
        }
    }

    /// <summary>Opens the vault with a key previously returned by <see cref="CreateDeviceKey"/>.</summary>
    /// <exception cref="VaultAuthenticationException">The key belongs to another account or another vault.</exception>
    public static PasswordVault OpenWithDeviceKey(string path, byte[] deviceKey) =>
        Open(path, _ => SecretKey.UnprotectForCurrentUser(deviceKey));

    public VaultEntry? Find(Guid id) => _entries.Find(e => e.Id == id);

    public void Upsert(VaultEntry entry) => Commit(entries =>
    {
        int index = entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            entries[index] = entry;
        }
        else
        {
            entries.Add(entry);
        }
    });

    public void AddRange(IEnumerable<VaultEntry> entries) => Commit(list => list.AddRange(entries));

    public void Remove(Guid id) => Commit(entries => entries.RemoveAll(e => e.Id == id));

    public void AddFolder(string name)
    {
        string folder = name.Trim();
        if (folder.Length > 0 && !Folders.Contains(folder, FolderComparer))
        {
            Commit(_ => _folders.Add(folder));
        }
    }

    public FolderStyle StyleOf(string folder) => _folderStyles.GetValueOrDefault(folder) ?? FolderStyle.Default;

    public void SetFolderStyle(string folder, FolderStyle style) => Commit(_ =>
    {
        if (style == FolderStyle.Default)
        {
            _folderStyles.Remove(folder);
        }
        else
        {
            _folderStyles[folder] = style;
        }
    });

    /// <summary>Renames the folder and moves its entries; renaming onto an existing folder merges the two.</summary>
    public void RenameFolder(string name, string newName)
    {
        string target = newName.Trim();
        if (target.Length == 0)
        {
            throw new ArgumentException("A folder needs a name.", nameof(newName));
        }

        Commit(entries =>
        {
            MoveEntries(entries, name, target);
            _folders.RemoveAll(f => FolderComparer.Equals(f, name));
            if (_folderStyles.Remove(name, out var style))
            {
                _folderStyles.TryAdd(target, style);
            }
            if (!_folders.Contains(target, FolderComparer))
            {
                _folders.Add(target);
            }
        });
    }

    /// <summary>Removes the folder; its entries stay in the vault without a folder.</summary>
    public void DeleteFolder(string name) => Commit(entries =>
    {
        MoveEntries(entries, name, "");
        _folders.RemoveAll(f => FolderComparer.Equals(f, name));
        _folderStyles.Remove(name);
    });

    /// <exception cref="VaultAuthenticationException"><paramref name="currentPassword"/> is wrong.</exception>
    public void ChangeMasterPassword(SecureString currentPassword, SecureString newPassword)
    {
        var slot = _header.Slot(KeySlotKind.Password);
        currentPassword.Use(slot.Unlock).Dispose();
        SetMasterPassword(newPassword);
    }

    /// <summary>Replaces the recovery key; the previous one stops working.</summary>
    public string RegenerateRecoveryKey()
    {
        string recoveryKey = RecoveryKey.Generate();
        SaveHeader(_header.With(CreateRecoverySlot(recoveryKey, _dataKey)));
        return recoveryKey;
    }

    /// <summary>
    /// The data key encrypted with Windows DPAPI: it unlocks this vault without the master password,
    /// but only for the current Windows account on this computer.
    /// </summary>
    public byte[] CreateDeviceKey() => _dataKey.ProtectForCurrentUser();

    public void Dispose() => _dataKey.Dispose();

    private static void MoveEntries(List<VaultEntry> entries, string from, string to)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (FolderComparer.Equals(entries[i].Folder, from))
            {
                entries[i] = entries[i] with { Folder = to, ModifiedAt = DateTimeOffset.UtcNow };
            }
        }
    }

    private static PasswordVault Open(string path, Func<VaultHeader, SecretKey> unlock)
    {
        var (header, headerBytes, payload) = VaultFile.Read(path);
        var dataKey = unlock(header);
        try
        {
            return new PasswordVault(path, header, dataKey, Decrypt(dataKey, headerBytes, payload));
        }
        catch
        {
            dataKey.Dispose();
            throw;
        }
    }

    private static PasswordVault Initialize(string path, VaultPayload payload, SecureString masterPassword, int passwordIterations)
    {
        var dataKey = SecretKey.Generate();
        try
        {
            string recoveryKey = RecoveryKey.Generate();
            var header = new VaultHeader(
            [
                masterPassword.Use(chars => KeySlot.Create(KeySlotKind.Password, chars, dataKey, passwordIterations)),
                CreateRecoverySlot(recoveryKey, dataKey),
            ]);
            var vault = new PasswordVault(path, header, dataKey, payload) { NewRecoveryKey = recoveryKey };
            vault.Save(header);
            return vault;
        }
        catch
        {
            dataKey.Dispose();
            throw;
        }
    }

    private static KeySlot CreateRecoverySlot(string recoveryKey, SecretKey dataKey) =>
        KeySlot.Create(KeySlotKind.RecoveryKey, RecoveryKey.Normalize(recoveryKey)!, dataKey, KeySlot.RecoveryKeyIterations);

    private void SetMasterPassword(SecureString password)
    {
        int iterations = _header.Slot(KeySlotKind.Password).Iterations;
        SaveHeader(_header.With(password.Use(chars => KeySlot.Create(KeySlotKind.Password, chars, _dataKey, iterations))));
    }

    private void SaveHeader(VaultHeader header)
    {
        Save(header);
        _header = header;
    }

    /// <summary>Applies a change and persists it, rolling the in-memory state back if the write fails.</summary>
    private void Commit(Action<List<VaultEntry>> mutate)
    {
        var entries = _entries.ToList();
        var folders = _folders.ToList();
        var styles = _folderStyles.ToList();
        mutate(_entries);
        try
        {
            Save(_header);
        }
        catch
        {
            _entries.Clear();
            _entries.AddRange(entries);
            _folders.Clear();
            _folders.AddRange(folders);
            _folderStyles.Clear();
            foreach (var (folder, style) in styles)
            {
                _folderStyles[folder] = style;
            }
            throw;
        }
    }

    private void Save(VaultHeader header)
    {
        byte[] headerBytes = header.ToBytes();
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(new VaultPayload(_entries, _folders, _folderStyles), VaultJsonContext.Default.VaultPayload);
        try
        {
            VaultFile.Write(_path, headerBytes, _dataKey.Seal(plaintext, headerBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static VaultPayload Decrypt(SecretKey dataKey, byte[] headerBytes, byte[] payload)
    {
        byte[] plaintext = dataKey.Open(payload, headerBytes);
        try
        {
            return JsonSerializer.Deserialize(plaintext, VaultJsonContext.Default.VaultPayload)
                ?? throw new InvalidDataException("The vault payload is empty.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
