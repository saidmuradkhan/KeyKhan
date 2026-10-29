#include "passin_native.h"

#include "crypto.h"
#include "totp.h"

#include <algorithm>
#include <climits>
#include <new>
#include <stdexcept>

struct kk_key {
    passin::SecureBuffer material;
};

namespace {

using namespace passin;

// The C boundary must never let a C++ exception escape.
template <typename Operation>
kk_status guarded(Operation&& operation) noexcept {
    try {
        operation();
        return KK_OK;
    } catch (const crypto::AuthenticationError&) {
        return KK_AUTHENTICATION_FAILED;
    } catch (const std::invalid_argument&) {
        return KK_INVALID_ARGUMENT;
    } catch (const std::bad_alloc&) {
        return KK_OUT_OF_MEMORY;
    } catch (...) {
        return KK_CRYPTO_FAILURE;
    }
}

template <typename T>
std::span<const T> view(const T* data, std::size_t length) {
    if (data == nullptr && length != 0) {
        throw std::invalid_argument("null buffer");
    }
    return {data, length};
}

template <typename T>
std::span<T> view(T* data, std::size_t length) {
    if (data == nullptr && length != 0) {
        throw std::invalid_argument("null buffer");
    }
    return {data, length};
}

SecureBuffer to_utf8(std::span<const wchar_t> text) {
    if (text.size() > INT_MAX) {
        throw std::invalid_argument("text too long");
    }
    const int source_length = static_cast<int>(text.size());
    const int required = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), source_length, nullptr, 0,
                                             nullptr, nullptr);
    if (required <= 0) {
        throw std::invalid_argument("password is not valid UTF-16");
    }
    SecureBuffer output(static_cast<std::size_t>(required));
    WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), source_length,
                        reinterpret_cast<char*>(output.data()), required, nullptr, nullptr);
    return output;
}

crypto::HashAlgorithm to_algorithm(kk_hash hash) {
    switch (hash) {
        case KK_HASH_SHA1: return crypto::HashAlgorithm::sha1;
        case KK_HASH_SHA256: return crypto::HashAlgorithm::sha256;
        case KK_HASH_SHA512: return crypto::HashAlgorithm::sha512;
    }
    throw std::invalid_argument("unknown hash algorithm");
}

const kk_key& require(const kk_key* key) {
    if (key == nullptr) {
        throw std::invalid_argument("null key");
    }
    return *key;
}

// Layout: nonce || ciphertext || tag.
void seal(std::span<const std::uint8_t> key, std::span<const std::uint8_t> plaintext,
          std::span<const std::uint8_t> aad, std::span<std::uint8_t> sealed) {
    if (sealed.size() != plaintext.size() + KK_SEAL_OVERHEAD) {
        throw std::invalid_argument("output size mismatch");
    }
    const auto nonce = sealed.first<KK_NONCE_SIZE>();
    crypto::random_bytes(nonce);
    crypto::aes_gcm_encrypt(key, nonce, aad, plaintext, sealed.subspan(KK_NONCE_SIZE, plaintext.size()),
                            sealed.last<KK_TAG_SIZE>());
}

void open(std::span<const std::uint8_t> key, std::span<const std::uint8_t> sealed,
          std::span<const std::uint8_t> aad, std::span<std::uint8_t> plaintext) {
    if (sealed.size() < KK_SEAL_OVERHEAD || plaintext.size() != sealed.size() - KK_SEAL_OVERHEAD) {
        throw std::invalid_argument("sealed size mismatch");
    }
    crypto::aes_gcm_decrypt(key, sealed.first<KK_NONCE_SIZE>(), aad, sealed.subspan(KK_NONCE_SIZE, plaintext.size()),
                            sealed.last<KK_TAG_SIZE>(), plaintext);
}

kk_key* adopt(SecureBuffer material) {
    if (material.size() != crypto::key_size) {
        throw std::invalid_argument("invalid key size");
    }
    return new kk_key{std::move(material)};
}

}  // namespace

extern "C" {

kk_status kk_derive_key(const wchar_t* password, size_t password_length, const uint8_t* salt, size_t salt_length,
                        uint32_t iterations, kk_key** key) {
    return guarded([&] {
        if (key == nullptr || password_length == 0 || salt_length < 16) {
            throw std::invalid_argument("invalid key derivation arguments");
        }
        *key = nullptr;
        const SecureBuffer utf8 = to_utf8(view(password, password_length));
        *key = adopt(crypto::pbkdf2_sha256(utf8.span(), view(salt, salt_length), iterations, crypto::key_size));
    });
}

void kk_key_free(kk_key* key) {
    delete key;
}

kk_status kk_key_generate(kk_key** key) {
    return guarded([&] {
        if (key == nullptr) {
            throw std::invalid_argument("null output");
        }
        SecureBuffer material(crypto::key_size);
        crypto::random_bytes(material.span());
        *key = adopt(std::move(material));
    });
}

kk_status kk_key_wrap(const kk_key* wrapping_key, const kk_key* key, const uint8_t* aad, size_t aad_length,
                      uint8_t* output, size_t output_length) {
    return guarded([&] {
        if (output_length != KK_WRAPPED_KEY_SIZE) {
            throw std::invalid_argument("output size mismatch");
        }
        seal(require(wrapping_key).material.span(), require(key).material.span(), view(aad, aad_length),
             view(output, output_length));
    });
}

kk_status kk_key_unwrap(const kk_key* wrapping_key, const uint8_t* wrapped, size_t wrapped_length,
                        const uint8_t* aad, size_t aad_length, kk_key** key) {
    return guarded([&] {
        if (key == nullptr || wrapped_length != KK_WRAPPED_KEY_SIZE) {
            throw std::invalid_argument("invalid wrapped key");
        }
        *key = nullptr;
        SecureBuffer material(crypto::key_size);
        open(require(wrapping_key).material.span(), view(wrapped, wrapped_length), view(aad, aad_length),
             material.span());
        *key = adopt(std::move(material));
    });
}

kk_status kk_key_protect_for_user(const kk_key* key, uint8_t** blob, size_t* blob_length) {
    return guarded([&] {
        if (blob == nullptr || blob_length == nullptr) {
            throw std::invalid_argument("null output");
        }
        const auto protected_key = crypto::protect_for_user(require(key).material.span());
        *blob = new uint8_t[protected_key.size()];
        std::copy(protected_key.begin(), protected_key.end(), *blob);
        *blob_length = protected_key.size();
    });
}

kk_status kk_key_unprotect_for_user(const uint8_t* blob, size_t blob_length, kk_key** key) {
    return guarded([&] {
        if (key == nullptr) {
            throw std::invalid_argument("null output");
        }
        *key = nullptr;
        *key = adopt(crypto::unprotect_for_user(view(blob, blob_length)));
    });
}

void kk_blob_free(uint8_t* blob) {
    delete[] blob;
}

kk_status kk_seal(const kk_key* key, const uint8_t* plaintext, size_t plaintext_length, const uint8_t* aad,
                  size_t aad_length, uint8_t* output, size_t output_length) {
    return guarded([&] {
        seal(require(key).material.span(), view(plaintext, plaintext_length), view(aad, aad_length),
             view(output, output_length));
    });
}

kk_status kk_open(const kk_key* key, const uint8_t* sealed, size_t sealed_length, const uint8_t* aad,
                  size_t aad_length, uint8_t* output, size_t output_length) {
    return guarded([&] {
        open(require(key).material.span(), view(sealed, sealed_length), view(aad, aad_length),
             view(output, output_length));
    });
}

kk_status kk_totp(const wchar_t* base32_secret, size_t secret_length, int64_t unix_seconds, uint32_t period,
                  uint32_t digits, kk_hash algorithm, uint32_t* code) {
    return guarded([&] {
        if (code == nullptr) {
            throw std::invalid_argument("null output");
        }
        const auto text = view(base32_secret, secret_length);
        const SecureBuffer secret = totp::decode_base32({text.data(), text.size()});
        *code = totp::generate(secret.span(), unix_seconds, period, digits, to_algorithm(algorithm));
    });
}

}  // extern "C"
