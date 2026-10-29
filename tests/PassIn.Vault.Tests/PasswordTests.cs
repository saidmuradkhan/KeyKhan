using PassIn.Vault.Passwords;

namespace PassIn.Vault.Tests;

public sealed class PasswordTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(128)]
    public void Generate_honours_length(int length) =>
        Assert.Equal(length, PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = length }).Length);

    [Fact]
    public void Generate_includes_every_enabled_set()
    {
        for (int i = 0; i < 200; i++)
        {
            string password = PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = 4 });

            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, c => !char.IsAsciiLetterOrDigit(c));
        }
    }

    [Fact]
    public void Generate_respects_disabled_sets_and_ambiguity()
    {
        var options = new PasswordGeneratorOptions { Length = 64, Uppercase = false, Symbols = false, ExcludeAmbiguous = true };

        string password = PasswordGenerator.Generate(options);

        Assert.All(password, c => Assert.True(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)));
        Assert.DoesNotContain(password, c => "l1o0".Contains(c));
    }

    [Fact]
    public void Generate_without_sets_throws() =>
        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(
            new PasswordGeneratorOptions { Uppercase = false, Lowercase = false, Digits = false, Symbols = false }));

    [Theory]
    [InlineData("", StrengthLevel.None)]
    [InlineData("aaaaaa", StrengthLevel.VeryWeak)]
    [InlineData("Summer2024", StrengthLevel.Fair)]
    [InlineData("k8#Vq!2zR@pL9$wX&m4N", StrengthLevel.VeryStrong)]
    public void Evaluate_grades_passwords(string password, StrengthLevel expected) =>
        Assert.Equal(expected, PasswordStrength.Evaluate(password));
}
