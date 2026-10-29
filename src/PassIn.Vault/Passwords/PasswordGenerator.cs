using System.Security.Cryptography;

namespace PassIn.Vault.Passwords;

public sealed record PasswordGeneratorOptions
{
    public int Length { get; init; } = 20;
    public bool Uppercase { get; init; } = true;
    public bool Lowercase { get; init; } = true;
    public bool Digits { get; init; } = true;
    public bool Symbols { get; init; } = true;
    public bool ExcludeAmbiguous { get; init; }
}

public static class PasswordGenerator
{
    public const int MinLength = 4;
    public const int MaxLength = 128;

    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*()-_=+[]{};:,.<>?/~";
    private const string AmbiguousChars = "Il1O0o|`'\"";

    /// <summary>Guarantees at least one character from every enabled set.</summary>
    public static string Generate(PasswordGeneratorOptions options)
    {
        string[] sets = CharacterSets(options).ToArray();
        if (sets.Length == 0)
        {
            throw new ArgumentException("At least one character set must be enabled.", nameof(options));
        }

        int length = Math.Clamp(options.Length, Math.Max(MinLength, sets.Length), MaxLength);
        string pool = string.Concat(sets);
        var chars = new char[length];

        for (int i = 0; i < length; i++)
        {
            string source = i < sets.Length ? sets[i] : pool;
            chars[i] = source[RandomNumberGenerator.GetInt32(source.Length)];
        }
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static IEnumerable<string> CharacterSets(PasswordGeneratorOptions options)
    {
        if (options.Uppercase) yield return Filter(UppercaseChars, options);
        if (options.Lowercase) yield return Filter(LowercaseChars, options);
        if (options.Digits) yield return Filter(DigitChars, options);
        if (options.Symbols) yield return Filter(SymbolChars, options);
    }

    private static string Filter(string set, PasswordGeneratorOptions options) =>
        options.ExcludeAmbiguous ? string.Concat(set.Where(c => !AmbiguousChars.Contains(c))) : set;
}
