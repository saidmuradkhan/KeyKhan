using System.Security.Cryptography;

namespace PassIn.Vault.Security;

/// <summary>
/// A 160-bit random key shown once to the user, e.g. <c>K7QD-3MZA-…</c>, that can reset a forgotten master password.
/// </summary>
public static class RecoveryKey
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int Bytes = 20;
    private const int Symbols = Bytes * 8 / 5;
    private const int GroupSize = 4;

    public static string Generate()
    {
        Span<byte> random = stackalloc byte[Bytes];
        RandomNumberGenerator.Fill(random);

        var symbols = new char[Symbols];
        int buffer = 0, bits = 0, index = 0;
        foreach (byte b in random)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                symbols[index++] = Alphabet[(buffer >> bits) & 31];
            }
        }
        CryptographicOperations.ZeroMemory(random);

        return string.Join('-', symbols.Chunk(GroupSize).Select(group => new string(group)));
    }

    /// <summary>Uppercase symbols without separators, or <c>null</c> if the input is not a recovery key.</summary>
    /// <remarks>Digits that never occur in the key are read as the letters they resemble (0→O, 1→I, 8→B).</remarks>
    public static string? Normalize(string? input)
    {
        string symbols = string.Concat((input ?? "")
            .Where(c => c is not ('-' or ' '))
            .Select(c => char.ToUpperInvariant(c) switch { '0' => 'O', '1' => 'I', '8' => 'B', var upper => upper }));
        return symbols.Length == Symbols && symbols.All(Alphabet.Contains) ? symbols : null;
    }
}
