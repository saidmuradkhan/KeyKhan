using System.Runtime.InteropServices;
using System.Security;

namespace KeyKhan.Vault.Security;

public delegate T CharSpanFunc<out T>(ReadOnlySpan<char> chars);

public static class SecureStringExtensions
{
    /// <summary>
    /// Exposes the characters through unmanaged memory that is zeroed right after use,
    /// so the secret never becomes an immutable managed string.
    /// </summary>
    public static unsafe T Use<T>(this SecureString value, CharSpanFunc<T> func)
    {
        nint pointer = Marshal.SecureStringToGlobalAllocUnicode(value);
        try
        {
            return func(new ReadOnlySpan<char>((void*)pointer, value.Length));
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
        }
    }

    public static unsafe bool ContentEquals(this SecureString value, SecureString other)
    {
        if (value.Length != other.Length)
        {
            return false;
        }

        nint first = Marshal.SecureStringToGlobalAllocUnicode(value);
        try
        {
            nint second = Marshal.SecureStringToGlobalAllocUnicode(other);
            try
            {
                return new ReadOnlySpan<char>((void*)first, value.Length)
                    .SequenceEqual(new ReadOnlySpan<char>((void*)second, other.Length));
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(second);
            }
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(first);
        }
    }
}
