using System.Buffers.Binary;

namespace KeyKhan.Vault.Storage;

/// <summary>
/// Plaintext file header. Its bytes are bound to the payload as AES-GCM associated data,
/// so tampering with any key slot makes decryption fail.
/// </summary>
/// <remarks>
/// Layout (little-endian): magic "KKVT" | u16 version | u8 slot count |
/// per slot: u8 kind | u32 iterations | u8 salt length | salt | wrapped data key.
/// </remarks>
internal sealed class VaultHeader
{
    public const ushort CurrentVersion = 2;

    private static ReadOnlySpan<byte> Magic => "KKVT"u8;
    private static readonly int WrappedKeySize = Interop.NativeMethods.WrappedKeySize;

    public VaultHeader(IReadOnlyList<KeySlot> slots) => Slots = slots;

    public IReadOnlyList<KeySlot> Slots { get; }

    public KeySlot Slot(KeySlotKind kind) =>
        Slots.FirstOrDefault(s => s.Kind == kind) ?? throw new InvalidDataException($"The vault has no {kind} slot.");

    public VaultHeader With(KeySlot slot) => new([.. Slots.Where(s => s.Kind != slot.Kind), slot]);

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(CurrentVersion);
        writer.Write((byte)Slots.Count);
        foreach (var slot in Slots)
        {
            writer.Write((byte)slot.Kind);
            writer.Write(slot.Iterations);
            writer.Write((byte)slot.Salt.Length);
            writer.Write(slot.Salt);
            writer.Write(slot.WrappedKey);
        }
        writer.Flush();
        return stream.ToArray();
    }

    /// <returns>The header and the number of bytes it occupies.</returns>
    public static (VaultHeader Header, int Size) Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 7 || !data[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("The file is not a KeyKhan vault.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        if (version == LegacyFormat.Version)
        {
            throw new InvalidDataException("This vault was created by KeyKhan 1.0. Unlock it once with your master password to upgrade it.");
        }
        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Vault format version {version} is not supported by this version of KeyKhan.");
        }

        int count = data[6];
        int offset = 7;
        var slots = new List<KeySlot>(count);
        for (int i = 0; i < count; i++)
        {
            if (data.Length < offset + 6)
            {
                throw Corrupted();
            }
            var kind = (KeySlotKind)data[offset];
            int iterations = BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 1)..]);
            int saltLength = data[offset + 5];
            offset += 6;

            if (iterations <= 0 || saltLength < 16 || data.Length < offset + saltLength + WrappedKeySize)
            {
                throw Corrupted();
            }
            byte[] salt = data.Slice(offset, saltLength).ToArray();
            offset += saltLength;
            byte[] wrapped = data.Slice(offset, WrappedKeySize).ToArray();
            offset += WrappedKeySize;

            slots.Add(new KeySlot(kind, iterations, salt, wrapped));
        }

        return (new VaultHeader(slots), offset);
    }

    private static InvalidDataException Corrupted() => new("The vault header is corrupted.");
}
