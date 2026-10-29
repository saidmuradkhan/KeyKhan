using System.Security.Cryptography;
using PassIn.Vault.Security;

namespace PassIn.Vault.Storage;

internal enum KeySlotKind : byte
{
    Password = 1,
    RecoveryKey = 2,
}

/// <summary>
/// The vault's data key, wrapped under a key derived (PBKDF2-HMAC-SHA256) from one secret.
/// Each slot opens the same vault, so a forgotten password can be replaced via the recovery slot.
/// </summary>
internal sealed record KeySlot(KeySlotKind Kind, int Iterations, byte[] Salt, byte[] WrappedKey)
{
    public const int PasswordIterations = 600_000;

    // A recovery key carries 160 random bits, so stretching it adds little.
    public const int RecoveryKeyIterations = 10_000;

    private const int SaltSize = 32;

    public static KeySlot Create(KeySlotKind kind, ReadOnlySpan<char> secret, SecretKey dataKey, int iterations)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var wrappingKey = SecretKey.Derive(secret, salt, iterations);
        return new KeySlot(kind, iterations, salt, wrappingKey.Wrap(dataKey, AssociatedData(kind)));
    }

    /// <exception cref="VaultAuthenticationException">The secret does not open this slot.</exception>
    public SecretKey Unlock(ReadOnlySpan<char> secret)
    {
        using var wrappingKey = SecretKey.Derive(secret, Salt, Iterations);
        return wrappingKey.Unwrap(WrappedKey, AssociatedData(Kind));
    }

    // Binds the wrapped key to its slot kind so slots cannot be swapped.
    private static byte[] AssociatedData(KeySlotKind kind) => [.. "KKVT-slot"u8, (byte)kind];
}
