namespace KeyKhan.Vault.Passwords;

public enum StrengthLevel
{
    None,
    VeryWeak,
    Weak,
    Fair,
    Strong,
    VeryStrong,
}

public static class PasswordStrength
{
    /// <summary>
    /// Entropy estimate from the character classes in use, discounted for repeated characters.
    /// </summary>
    public static double EstimateBits(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
        {
            return 0;
        }

        bool upper = false, lower = false, digit = false, symbol = false, other = false;
        var distinct = new HashSet<char>();
        foreach (char c in password)
        {
            distinct.Add(c);
            if (char.IsAsciiLetterUpper(c)) upper = true;
            else if (char.IsAsciiLetterLower(c)) lower = true;
            else if (char.IsAsciiDigit(c)) digit = true;
            else if (char.IsAscii(c)) symbol = true;
            else other = true;
        }

        int pool = (upper ? 26 : 0) + (lower ? 26 : 0) + (digit ? 10 : 0) + (symbol ? 33 : 0) + (other ? 100 : 0);
        double uniqueness = (double)distinct.Count / password.Length;
        double effectiveLength = password.Length * Math.Min(1, 0.4 + uniqueness);
        return effectiveLength * Math.Log2(pool);
    }

    public static StrengthLevel Evaluate(ReadOnlySpan<char> password) => EstimateBits(password) switch
    {
        0 => StrengthLevel.None,
        < 36 => StrengthLevel.VeryWeak,
        < 50 => StrengthLevel.Weak,
        < 70 => StrengthLevel.Fair,
        < 100 => StrengthLevel.Strong,
        _ => StrengthLevel.VeryStrong,
    };
}
