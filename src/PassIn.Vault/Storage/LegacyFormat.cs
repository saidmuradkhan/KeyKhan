using System.Buffers.Binary;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using PassIn.Vault.Security;

namespace PassIn.Vault.Storage;

/// <summary>
/// Reads vaults written by PassIn 1.0.x, where the master password key encrypted the entries directly.
/// </summary>
/// <remarks>Layout: magic "KKVT" | u16 version (1) | u8 kdf | u32 iterations | u8 salt length | salt | sealed payload.</remarks>
internal static class LegacyFormat
{
    public const ushort Version = 1;
    private const int FixedSize = 12;

    public static bool IsLegacy(ReadOnlySpan<byte> content) =>
        content.Length >= FixedSize && content[..4].SequenceEqual("KKVT"u8) &&
        BinaryPrimitives.ReadUInt16LittleEndian(content[4..]) == Version;

    /// <exception cref="VaultAuthenticationException">Wrong master password or damaged file.</exception>
    public static VaultPayload Decrypt(byte[] content, SecureString masterPassword)
    {
        int iterations = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(7));
        int saltLength = content[11];
        int headerSize = FixedSize + saltLength;
        if (iterations <= 0 || saltLength < 16 || content.Length < headerSize)
        {
            throw new InvalidDataException("The vault header is corrupted.");
        }

        byte[] salt = content[FixedSize..headerSize];
        using var key = SecretKey.Derive(masterPassword, salt, iterations);
        byte[] plaintext = key.Open(content.AsSpan(headerSize), content.AsSpan(0, headerSize));
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
