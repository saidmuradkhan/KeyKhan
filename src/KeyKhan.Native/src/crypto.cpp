#include "crypto.h"

#include <climits>
#include <utility>

namespace keykhan::crypto {
namespace {

constexpr NTSTATUS status_auth_tag_mismatch = static_cast<NTSTATUS>(0xC000A002L);

void check(NTSTATUS status, const char* operation) {
    if (!BCRYPT_SUCCESS(status)) {
        throw CryptoError(operation, status);
    }
}

ULONG to_ulong(std::size_t value) {
    if (value > ULONG_MAX) {
        throw std::invalid_argument("buffer exceeds CNG size limit");
    }
    return static_cast<ULONG>(value);
}

// CNG declares read-only input buffers as non-const.
PUCHAR input(std::span<const std::uint8_t> buffer) noexcept {
    return const_cast<PUCHAR>(buffer.data());
}

class KeyHandle {
public:
    explicit KeyHandle(std::span<const std::uint8_t> key) {
        check(BCryptGenerateSymmetricKey(BCRYPT_AES_GCM_ALG_HANDLE, &handle_, nullptr, 0, input(key),
                                         to_ulong(key.size()), 0),
              "BCryptGenerateSymmetricKey");
    }
    ~KeyHandle() { BCryptDestroyKey(handle_); }

    KeyHandle(const KeyHandle&) = delete;
    KeyHandle& operator=(const KeyHandle&) = delete;

    [[nodiscard]] BCRYPT_KEY_HANDLE get() const noexcept { return handle_; }

private:
    BCRYPT_KEY_HANDLE handle_ = nullptr;
};

BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO auth_info(std::span<const std::uint8_t> nonce,
                                                std::span<const std::uint8_t> aad,
                                                std::span<const std::uint8_t> tag) {
    if (nonce.size() != nonce_size || tag.size() != tag_size) {
        throw std::invalid_argument("invalid nonce or tag size");
    }
    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO info;
    BCRYPT_INIT_AUTH_MODE_INFO(info);
    info.pbNonce = input(nonce);
    info.cbNonce = to_ulong(nonce.size());
    info.pbAuthData = input(aad);
    info.cbAuthData = to_ulong(aad.size());
    info.pbTag = input(tag);
    info.cbTag = to_ulong(tag.size());
    return info;
}

BCRYPT_ALG_HANDLE hmac_provider(HashAlgorithm algorithm) {
    switch (algorithm) {
        case HashAlgorithm::sha1: return BCRYPT_HMAC_SHA1_ALG_HANDLE;
        case HashAlgorithm::sha256: return BCRYPT_HMAC_SHA256_ALG_HANDLE;
        case HashAlgorithm::sha512: return BCRYPT_HMAC_SHA512_ALG_HANDLE;
    }
    throw std::invalid_argument("unknown hash algorithm");
}

}  // namespace

void random_bytes(std::span<std::uint8_t> output) {
    check(BCryptGenRandom(nullptr, output.data(), to_ulong(output.size()), BCRYPT_USE_SYSTEM_PREFERRED_RNG),
          "BCryptGenRandom");
}

SecureBuffer pbkdf2_sha256(std::span<const std::uint8_t> password, std::span<const std::uint8_t> salt,
                           std::uint32_t iterations, std::size_t length) {
    if (iterations == 0) {
        throw std::invalid_argument("iterations must be positive");
    }
    SecureBuffer output(length);
    check(BCryptDeriveKeyPBKDF2(BCRYPT_HMAC_SHA256_ALG_HANDLE, input(password), to_ulong(password.size()),
                                input(salt), to_ulong(salt.size()), iterations, output.data(),
                                to_ulong(output.size()), 0),
          "BCryptDeriveKeyPBKDF2");
    return output;
}

void aes_gcm_encrypt(std::span<const std::uint8_t> key, std::span<const std::uint8_t> nonce,
                     std::span<const std::uint8_t> aad, std::span<const std::uint8_t> plaintext,
                     std::span<std::uint8_t> ciphertext, std::span<std::uint8_t> tag) {
    if (ciphertext.size() != plaintext.size()) {
        throw std::invalid_argument("ciphertext size must match plaintext size");
    }
    const KeyHandle handle(key);
    auto info = auth_info(nonce, aad, tag);
    ULONG written = 0;
    check(BCryptEncrypt(handle.get(), input(plaintext), to_ulong(plaintext.size()), &info, nullptr, 0,
                        ciphertext.data(), to_ulong(ciphertext.size()), &written, 0),
          "BCryptEncrypt");
}

void aes_gcm_decrypt(std::span<const std::uint8_t> key, std::span<const std::uint8_t> nonce,
                     std::span<const std::uint8_t> aad, std::span<const std::uint8_t> ciphertext,
                     std::span<const std::uint8_t> tag, std::span<std::uint8_t> plaintext) {
    if (plaintext.size() != ciphertext.size()) {
        throw std::invalid_argument("plaintext size must match ciphertext size");
    }
    const KeyHandle handle(key);
    auto info = auth_info(nonce, aad, tag);
    ULONG written = 0;
    const NTSTATUS status = BCryptDecrypt(handle.get(), input(ciphertext), to_ulong(ciphertext.size()), &info,
                                          nullptr, 0, plaintext.data(), to_ulong(plaintext.size()), &written, 0);
    if (status == status_auth_tag_mismatch) {
        SecureZeroMemory(plaintext.data(), plaintext.size());
        throw AuthenticationError();
    }
    check(status, "BCryptDecrypt");
}

std::size_t digest_size(HashAlgorithm algorithm) {
    switch (algorithm) {
        case HashAlgorithm::sha1: return 20;
        case HashAlgorithm::sha256: return 32;
        case HashAlgorithm::sha512: return 64;
    }
    throw std::invalid_argument("unknown hash algorithm");
}

void hmac(HashAlgorithm algorithm, std::span<const std::uint8_t> key, std::span<const std::uint8_t> message,
          std::span<std::uint8_t> output) {
    if (output.size() != digest_size(algorithm)) {
        throw std::invalid_argument("output size must match digest size");
    }
    check(BCryptHash(hmac_provider(algorithm), input(key), to_ulong(key.size()), input(message),
                     to_ulong(message.size()), output.data(), to_ulong(output.size())),
          "BCryptHash");
}

}  // namespace keykhan::crypto
