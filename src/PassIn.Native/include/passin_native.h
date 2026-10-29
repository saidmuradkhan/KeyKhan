#pragma once

#include <stddef.h>
#include <stdint.h>

#ifdef PASSIN_NATIVE_EXPORTS
#define KK_API __declspec(dllexport)
#else
#define KK_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef enum kk_status {
    KK_OK = 0,
    KK_INVALID_ARGUMENT = 1,
    KK_AUTHENTICATION_FAILED = 2,
    KK_OUT_OF_MEMORY = 3,
    KK_CRYPTO_FAILURE = 4,
} kk_status;

typedef enum kk_hash {
    KK_HASH_SHA1 = 0,
    KK_HASH_SHA256 = 1,
    KK_HASH_SHA512 = 2,
} kk_hash;

/* AES-256 key derived from the master password. Lives in locked, wiped-on-free memory. */
typedef struct kk_key kk_key;

#define KK_NONCE_SIZE 12
#define KK_TAG_SIZE 16
#define KK_SEAL_OVERHEAD (KK_NONCE_SIZE + KK_TAG_SIZE)

/* PBKDF2-HMAC-SHA256 over the UTF-8 encoding of a UTF-16 password. */
KK_API kk_status kk_derive_key(const wchar_t* password, size_t password_length,
                               const uint8_t* salt, size_t salt_length,
                               uint32_t iterations, kk_key** key);

KK_API void kk_key_free(kk_key* key);

/* Fresh random 256-bit key, used as the vault's data key. */
KK_API kk_status kk_key_generate(kk_key** key);

#define KK_WRAPPED_KEY_SIZE (32 + KK_SEAL_OVERHEAD)

/* Encrypts `key` under `wrapping_key` (AES-256-GCM). output_length must be KK_WRAPPED_KEY_SIZE. */
KK_API kk_status kk_key_wrap(const kk_key* wrapping_key, const kk_key* key,
                             const uint8_t* aad, size_t aad_length,
                             uint8_t* output, size_t output_length);

/* Returns KK_AUTHENTICATION_FAILED when `wrapping_key` or `aad` does not match. */
KK_API kk_status kk_key_unwrap(const kk_key* wrapping_key,
                               const uint8_t* wrapped, size_t wrapped_length,
                               const uint8_t* aad, size_t aad_length, kk_key** key);

/* DPAPI: binds the key to the current Windows account. Release the blob with kk_blob_free. */
KK_API kk_status kk_key_protect_for_user(const kk_key* key, uint8_t** blob, size_t* blob_length);

KK_API kk_status kk_key_unprotect_for_user(const uint8_t* blob, size_t blob_length, kk_key** key);

KK_API void kk_blob_free(uint8_t* blob);

/* AES-256-GCM. Output layout: nonce || ciphertext || tag.
   output_length must equal plaintext_length + KK_SEAL_OVERHEAD. */
KK_API kk_status kk_seal(const kk_key* key,
                         const uint8_t* plaintext, size_t plaintext_length,
                         const uint8_t* aad, size_t aad_length,
                         uint8_t* output, size_t output_length);

/* output_length must equal sealed_length - KK_SEAL_OVERHEAD.
   Returns KK_AUTHENTICATION_FAILED on a wrong key or tampered data. */
KK_API kk_status kk_open(const kk_key* key,
                         const uint8_t* sealed, size_t sealed_length,
                         const uint8_t* aad, size_t aad_length,
                         uint8_t* output, size_t output_length);

/* RFC 6238 time-based one-time password from a Base32 secret. */
KK_API kk_status kk_totp(const wchar_t* base32_secret, size_t secret_length,
                         int64_t unix_seconds, uint32_t period, uint32_t digits,
                         kk_hash algorithm, uint32_t* code);

#ifdef __cplusplus
}
#endif
