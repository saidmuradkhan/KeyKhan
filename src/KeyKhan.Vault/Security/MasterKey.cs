using System.Security;
using KeyKhan.Vault.Interop;

namespace KeyKhan.Vault.Security;

/// <summary>
/// AES-256-GCM key derived from the master password. The key bytes stay inside the native core.
/// </summary>
public sealed class MasterKey : IDisposable
{
    private readonly SafeKeyHandle _handle;

    private MasterKey(SafeKeyHandle handle) => _handle = handle;

    public static unsafe MasterKey Derive(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, int iterations)
    {
        if (password.IsEmpty)
        {
            throw new ArgumentException("The password must not be empty.", nameof(password));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        fixed (char* passwordPointer = password)
        fixed (byte* saltPointer = salt)
        {
            NativeMethods.kk_derive_key(
                    passwordPointer, (nuint)password.Length, saltPointer, (nuint)salt.Length, (uint)iterations, out var handle)
                .ThrowIfFailed();
            return new MasterKey(handle);
        }
    }

    public static MasterKey Derive(SecureString password, byte[] salt, int iterations) =>
        password.Use(chars => Derive(chars, salt, iterations));

    public unsafe byte[] Seal(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        var output = new byte[plaintext.Length + NativeMethods.SealOverhead];
        fixed (byte* plaintextPointer = plaintext)
        fixed (byte* aadPointer = associatedData)
        fixed (byte* outputPointer = output)
        {
            NativeMethods.kk_seal(
                    _handle,
                    plaintextPointer, (nuint)plaintext.Length,
                    aadPointer, (nuint)associatedData.Length,
                    outputPointer, (nuint)output.Length)
                .ThrowIfFailed();
        }
        return output;
    }

    /// <exception cref="VaultAuthenticationException">The key or associated data does not match.</exception>
    public unsafe byte[] Open(ReadOnlySpan<byte> sealedData, ReadOnlySpan<byte> associatedData)
    {
        if (sealedData.Length < NativeMethods.SealOverhead)
        {
            throw new VaultAuthenticationException();
        }

        var output = new byte[sealedData.Length - NativeMethods.SealOverhead];
        fixed (byte* sealedPointer = sealedData)
        fixed (byte* aadPointer = associatedData)
        fixed (byte* outputPointer = output)
        {
            NativeMethods.kk_open(
                    _handle,
                    sealedPointer, (nuint)sealedData.Length,
                    aadPointer, (nuint)associatedData.Length,
                    outputPointer, (nuint)output.Length)
                .ThrowIfFailed();
        }
        return output;
    }

    public void Dispose() => _handle.Dispose();
}
