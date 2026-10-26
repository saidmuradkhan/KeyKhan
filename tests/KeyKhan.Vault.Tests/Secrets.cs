using System.Security;

namespace KeyKhan.Vault.Tests;

internal static class Secrets
{
    public static SecureString Of(string value)
    {
        var secure = new SecureString();
        foreach (char c in value)
        {
            secure.AppendChar(c);
        }
        secure.MakeReadOnly();
        return secure;
    }
}
