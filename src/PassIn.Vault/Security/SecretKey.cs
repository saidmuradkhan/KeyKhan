using System.Security;
using PassIn.Vault.Interop;

namespace PassIn.Vault.Security;

/// <summary>
/// AES-256-GCM key held by the native core. Managed code only ever sees an opaque handle.
/// </summary>
public sealed class SecretKey : IDisposable
{
    private readonly SafeKeyHandle _handle;

    private SecretKey(SafeKeyHandle handle) => _handle = handle;

    /// <summary>PBKDF2-HMAC-SHA256 over the UTF-8 encoding of <paramref name="password"/>.</summary>
    public static unsafe SecretKey Derive(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, int iterations)
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
            return new SecretKey(handle);
        }
    }

    public static SecretKey Derive(SecureString password, byte[] salt, int iterations) =>
        password.Use(chars => Derive(chars, salt, iterations));

    public static SecretKey Generate()
    {
        NativeMethods.kk_key_generate(out var handle).ThrowIfFailed();
        return new SecretKey(handle);
    }

    /// <exception cref="VaultAuthenticationException">The blob belongs to another Windows account or was altered.</exception>
    public static unsafe SecretKey UnprotectForCurrentUser(ReadOnlySpan<byte> blob)
    {
        fixed (byte* blobPointer = blob)
        {
            NativeMethods.kk_key_unprotect_for_user(blobPointer, (nuint)blob.Length, out var handle).ThrowIfFailed();
            return new SecretKey(handle);
        }
    }

    /// <summary>Encrypts this key with Windows DPAPI so only the current Windows account can recover it.</summary>
    public unsafe byte[] ProtectForCurrentUser()
    {
        NativeMethods.kk_key_protect_for_user(_handle, out byte* blob, out nuint length).ThrowIfFailed();
        try
        {
            return new ReadOnlySpan<byte>(blob, checked((int)length)).ToArray();
        }
        finally
        {
            NativeMethods.kk_blob_free(blob);
        }
    }

    /// <summary>Encrypts <paramref name="key"/> under this key without exposing its bytes.</summary>
    public unsafe byte[] Wrap(SecretKey key, ReadOnlySpan<byte> associatedData)
    {
        var output = new byte[NativeMethods.WrappedKeySize];
        fixed (byte* aadPointer = associatedData)
        fixed (byte* outputPointer = output)
        {
            NativeMethods.kk_key_wrap(
                    _handle, key._handle, aadPointer, (nuint)associatedData.Length, outputPointer, (nuint)output.Length)
                .ThrowIfFailed();
        }
        return output;
    }

    /// <exception cref="VaultAuthenticationException">This key or the associated data does not match.</exception>
    public unsafe SecretKey Unwrap(ReadOnlySpan<byte> wrapped, ReadOnlySpan<byte> associatedData)
    {
        if (wrapped.Length != NativeMethods.WrappedKeySize)
        {
            throw new VaultAuthenticationException();
        }

        fixed (byte* wrappedPointer = wrapped)
        fixed (byte* aadPointer = associatedData)
        {
            NativeMethods.kk_key_unwrap(
                    _handle, wrappedPointer, (nuint)wrapped.Length, aadPointer, (nuint)associatedData.Length, out var handle)
                .ThrowIfFailed();
            return new SecretKey(handle);
        }
    }

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
