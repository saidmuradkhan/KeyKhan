namespace PassIn.Vault.Storage;

internal static class VaultFile
{
    public static (VaultHeader Header, byte[] HeaderBytes, byte[] Payload) Read(string path)
    {
        byte[] content = File.ReadAllBytes(path);
        var (header, size) = VaultHeader.Parse(content);
        return (header, content[..size], content[size..]);
    }

    /// <summary>Writes to a sibling temp file first so a crash never leaves a half-written vault.</summary>
    public static void Write(string path, ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(header);
            stream.Write(payload);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }
}
