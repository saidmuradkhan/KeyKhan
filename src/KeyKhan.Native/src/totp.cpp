#include "totp.h"

#include <array>
#include <stdexcept>

namespace keykhan::totp {
namespace {

int base32_value(wchar_t c) noexcept {
    if (c >= L'A' && c <= L'Z') return c - L'A';
    if (c >= L'a' && c <= L'z') return c - L'a';
    if (c >= L'2' && c <= L'7') return c - L'2' + 26;
    return -1;
}

bool is_ignored(wchar_t c) noexcept {
    return c == L' ' || c == L'-' || c == L'=' || c == L'\t';
}

constexpr std::array<std::uint32_t, 9> powers_of_ten = {1, 10, 100, 1'000, 10'000, 100'000, 1'000'000,
                                                       10'000'000, 100'000'000};

}  // namespace

SecureBuffer decode_base32(std::wstring_view text) {
    std::size_t symbols = 0;
    for (const wchar_t c : text) {
        if (is_ignored(c)) continue;
        if (base32_value(c) < 0) throw std::invalid_argument("invalid Base32 character");
        ++symbols;
    }
    if (symbols == 0) {
        throw std::invalid_argument("empty secret");
    }

    SecureBuffer output(symbols * 5 / 8);
    std::uint32_t buffer = 0;
    int bits = 0;
    std::size_t written = 0;
    for (const wchar_t c : text) {
        if (is_ignored(c)) continue;
        buffer = (buffer << 5) | static_cast<std::uint32_t>(base32_value(c));
        bits += 5;
        if (bits >= 8) {
            bits -= 8;
            output.data()[written++] = static_cast<std::uint8_t>(buffer >> bits);
        }
    }
    SecureZeroMemory(&buffer, sizeof buffer);
    return output;
}

std::uint32_t generate(std::span<const std::uint8_t> secret, std::int64_t unix_seconds, std::uint32_t period,
                       std::uint32_t digits, crypto::HashAlgorithm algorithm) {
    if (secret.empty() || period == 0 || unix_seconds < 0 || digits < 6 || digits > 8) {
        throw std::invalid_argument("invalid TOTP parameters");
    }

    auto counter = static_cast<std::uint64_t>(unix_seconds) / period;
    std::array<std::uint8_t, 8> message{};
    for (auto it = message.rbegin(); it != message.rend(); ++it) {
        *it = static_cast<std::uint8_t>(counter & 0xFF);
        counter >>= 8;
    }

    std::array<std::uint8_t, 64> digest{};
    const auto mac = std::span(digest).first(crypto::digest_size(algorithm));
    crypto::hmac(algorithm, secret, message, mac);

    const std::size_t offset = mac.back() & 0x0F;
    const std::uint32_t binary = (static_cast<std::uint32_t>(mac[offset] & 0x7F) << 24) |
                                 (static_cast<std::uint32_t>(mac[offset + 1]) << 16) |
                                 (static_cast<std::uint32_t>(mac[offset + 2]) << 8) |
                                 static_cast<std::uint32_t>(mac[offset + 3]);
    SecureZeroMemory(digest.data(), digest.size());
    return binary % powers_of_ten[digits];
}

}  // namespace keykhan::totp
