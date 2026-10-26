using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using KeyKhan.Vault.Security;
using KeyKhan.Vault.Storage;

namespace KeyKhan.Vault;

/// <summary>
/// An unlocked vault. Every mutation is encrypted and persisted immediately.
/// </summary>
public sealed class PasswordVault : IDisposable
{
    private readonly string _path;
    private readonly List<VaultEntry> _entries;
    private VaultHeader _header;
    private byte[] _headerBytes;
    private MasterKey _key;

    private PasswordVault(string path, VaultHeader header, byte[] headerBytes, MasterKey key, List<VaultEntry> entries)
    {
        _path = path;
        _header = header;
        _headerBytes = headerBytes;
        _key = key;
        _entries = entries;
    }

    public IReadOnlyList<VaultEntry> Entries => _entries;

    public static PasswordVault Create(string path, SecureString masterPassword) =>
        Create(path, masterPassword, VaultHeader.DefaultIterations);

    internal static PasswordVault Create(string path, SecureString masterPassword, int iterations)
    {
        if (File.Exists(path))
        {
            throw new IOException($"A vault already exists at '{path}'.");
        }

        var header = VaultHeader.CreateNew(iterations);
        var vault = new PasswordVault(path, header, header.ToBytes(), MasterKey.Derive(masterPassword, header.Salt, header.Iterations), []);
        vault.Save();
        return vault;
    }

    /// <exception cref="VaultAuthenticationException">Wrong master password or damaged file.</exception>
    public static PasswordVault Open(string path, SecureString masterPassword)
    {
        var (header, headerBytes, payload) = VaultFile.Read(path);
        var key = MasterKey.Derive(masterPassword, header.Salt, header.Iterations);
        try
        {
            return new PasswordVault(path, header, headerBytes, key, Decrypt(key, headerBytes, payload));
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

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

    /// <exception cref="VaultAuthenticationException"><paramref name="currentPassword"/> is wrong.</exception>
    public void ChangeMasterPassword(SecureString currentPassword, SecureString newPassword)
    {
        using (var check = MasterKey.Derive(currentPassword, _header.Salt, _header.Iterations))
        {
            var (_, storedHeader, payload) = VaultFile.Read(_path);
            check.Open(payload, storedHeader);
        }

        var header = VaultHeader.CreateNew(_header.Iterations);
        byte[] headerBytes = header.ToBytes();
        var key = MasterKey.Derive(newPassword, header.Salt, header.Iterations);
        try
        {
            Save(headerBytes, key);
        }
        catch
        {
            key.Dispose();
            throw;
        }

        _key.Dispose();
        (_header, _headerBytes, _key) = (header, headerBytes, key);
    }

    public void Dispose() => _key.Dispose();

    /// <summary>Applies a change and persists it, rolling the in-memory state back if the write fails.</summary>
    private void Commit(Action<List<VaultEntry>> mutate)
    {
        var snapshot = _entries.ToList();
        mutate(_entries);
        try
        {
            Save();
        }
        catch
        {
            _entries.Clear();
            _entries.AddRange(snapshot);
            throw;
        }
    }

    private void Save() => Save(_headerBytes, _key);

    private void Save(byte[] headerBytes, MasterKey key)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(new VaultPayload(_entries), VaultJsonContext.Default.VaultPayload);
        try
        {
            VaultFile.Write(_path, headerBytes, key.Seal(plaintext, headerBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static List<VaultEntry> Decrypt(MasterKey key, byte[] headerBytes, byte[] payload)
    {
        byte[] plaintext = key.Open(payload, headerBytes);
        try
        {
            return JsonSerializer.Deserialize(plaintext, VaultJsonContext.Default.VaultPayload)?.Entries
                ?? throw new InvalidDataException("The vault payload is empty.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
