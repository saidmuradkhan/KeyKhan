using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace KeyKhan.Vault.Interop;

internal enum NativeStatus
{
    Ok = 0,
    InvalidArgument = 1,
    AuthenticationFailed = 2,
    OutOfMemory = 3,
    CryptoFailure = 4,
}

internal enum NativeHash
{
    Sha1 = 0,
    Sha256 = 1,
    Sha512 = 2,
}

internal sealed class SafeKeyHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeKeyHandle() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.kk_key_free(handle);
        return true;
    }
}

internal static unsafe partial class NativeMethods
{
    private const string Library = "keykhan_native";

    public const int SealOverhead = 12 + 16;
    public const int WrappedKeySize = 32 + SealOverhead;

    [LibraryImport(Library)]
    public static partial NativeStatus kk_derive_key(
        char* password, nuint passwordLength, byte* salt, nuint saltLength, uint iterations, out SafeKeyHandle key);

    [LibraryImport(Library)]
    public static partial void kk_key_free(nint key);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_key_generate(out SafeKeyHandle key);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_key_wrap(
        SafeKeyHandle wrappingKey, SafeKeyHandle key, byte* aad, nuint aadLength, byte* output, nuint outputLength);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_key_unwrap(
        SafeKeyHandle wrappingKey, byte* wrapped, nuint wrappedLength, byte* aad, nuint aadLength, out SafeKeyHandle key);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_key_protect_for_user(SafeKeyHandle key, out byte* blob, out nuint blobLength);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_key_unprotect_for_user(byte* blob, nuint blobLength, out SafeKeyHandle key);

    [LibraryImport(Library)]
    public static partial void kk_blob_free(byte* blob);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_seal(
        SafeKeyHandle key, byte* plaintext, nuint plaintextLength, byte* aad, nuint aadLength, byte* output, nuint outputLength);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_open(
        SafeKeyHandle key, byte* sealedData, nuint sealedLength, byte* aad, nuint aadLength, byte* output, nuint outputLength);

    [LibraryImport(Library)]
    public static partial NativeStatus kk_totp(
        char* secret, nuint secretLength, long unixSeconds, uint period, uint digits, NativeHash algorithm, out uint code);

    public static void ThrowIfFailed(this NativeStatus status)
    {
        switch (status)
        {
            case NativeStatus.Ok:
                return;
            case NativeStatus.AuthenticationFailed:
                throw new VaultAuthenticationException();
            case NativeStatus.InvalidArgument:
                throw new ArgumentException("The native core rejected the arguments.");
            case NativeStatus.OutOfMemory:
                throw new OutOfMemoryException();
            default:
                throw new CryptographicException("The native cryptographic operation failed.");
        }
    }
}
