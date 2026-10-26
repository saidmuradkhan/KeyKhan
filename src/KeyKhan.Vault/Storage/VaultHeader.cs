using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KeyKhan.Vault.Storage;

/// <summary>
/// Plaintext file header. Its bytes are bound to the payload as AES-GCM associated data,
/// so tampering with the KDF parameters makes decryption fail.
/// </summary>
/// <remarks>
/// Layout (little-endian): magic "KKVT" | u16 version | u8 kdf | u32 iterations | u8 salt length | salt.
/// </remarks>
internal sealed class VaultHeader
{
    public const ushort CurrentVersion = 1;
    public const int DefaultIterations = 600_000;

    private const byte KdfPbkdf2Sha256 = 1;
    private const int SaltSize = 32;
    private const int FixedSize = 4 + 2 + 1 + 4 + 1;

    private static ReadOnlySpan<byte> Magic => "KKVT"u8;

    private VaultHeader(int iterations, byte[] salt)
    {
        Iterations = iterations;
        Salt = salt;
    }

    public int Iterations { get; }
    public byte[] Salt { get; }
    public int Size => FixedSize + Salt.Length;

    public static VaultHeader CreateNew(int iterations = DefaultIterations) =>
        new(iterations, RandomNumberGenerator.GetBytes(SaltSize));

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        var span = bytes.AsSpan();
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], CurrentVersion);
        span[6] = KdfPbkdf2Sha256;
        BinaryPrimitives.WriteInt32LittleEndian(span[7..], Iterations);
        span[11] = (byte)Salt.Length;
        Salt.CopyTo(span[FixedSize..]);
        return bytes;
    }

    public static VaultHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < FixedSize || !data[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("The file is not a KeyKhan vault.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Vault format version {version} is not supported by this version of KeyKhan.");
        }

        int iterations = BinaryPrimitives.ReadInt32LittleEndian(data[7..]);
        int saltLength = data[11];
        if (data[6] != KdfPbkdf2Sha256 || iterations <= 0 || saltLength < 16 || data.Length < FixedSize + saltLength)
        {
            throw new InvalidDataException("The vault header is corrupted.");
        }

        return new VaultHeader(iterations, data.Slice(FixedSize, saltLength).ToArray());
    }
}
