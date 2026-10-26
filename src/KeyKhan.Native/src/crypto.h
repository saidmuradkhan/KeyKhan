#pragma once

#include "secure_buffer.h"

#include <bcrypt.h>

#include <cstddef>
#include <cstdint>
#include <span>
#include <stdexcept>

namespace keykhan::crypto {

inline constexpr std::size_t key_size = 32;
inline constexpr std::size_t nonce_size = 12;
inline constexpr std::size_t tag_size = 16;

enum class HashAlgorithm { sha1, sha256, sha512 };

class CryptoError : public std::runtime_error {
public:
    CryptoError(const char* operation, NTSTATUS status) : std::runtime_error(operation), status_(status) {}
    [[nodiscard]] NTSTATUS status() const noexcept { return status_; }

private:
    NTSTATUS status_;
};

// Raised when an authenticated decryption fails: wrong key, wrong associated data or tampered input.
class AuthenticationError : public std::runtime_error {
public:
    AuthenticationError() : std::runtime_error("authentication tag mismatch") {}
};

void random_bytes(std::span<std::uint8_t> output);

SecureBuffer pbkdf2_sha256(std::span<const std::uint8_t> password, std::span<const std::uint8_t> salt,
                           std::uint32_t iterations, std::size_t length);

void aes_gcm_encrypt(std::span<const std::uint8_t> key, std::span<const std::uint8_t> nonce,
                     std::span<const std::uint8_t> aad, std::span<const std::uint8_t> plaintext,
                     std::span<std::uint8_t> ciphertext, std::span<std::uint8_t> tag);

void aes_gcm_decrypt(std::span<const std::uint8_t> key, std::span<const std::uint8_t> nonce,
                     std::span<const std::uint8_t> aad, std::span<const std::uint8_t> ciphertext,
                     std::span<const std::uint8_t> tag, std::span<std::uint8_t> plaintext);

std::size_t digest_size(HashAlgorithm algorithm);

void hmac(HashAlgorithm algorithm, std::span<const std::uint8_t> key, std::span<const std::uint8_t> message,
          std::span<std::uint8_t> output);

}  // namespace keykhan::crypto
