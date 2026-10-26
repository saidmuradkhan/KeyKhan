#pragma once

#include "crypto.h"

#include <cstdint>
#include <string_view>

namespace keykhan::totp {

// Decodes RFC 4648 Base32, ignoring case, whitespace, dashes and padding.
SecureBuffer decode_base32(std::wstring_view text);

std::uint32_t generate(std::span<const std::uint8_t> secret, std::int64_t unix_seconds, std::uint32_t period,
                       std::uint32_t digits, crypto::HashAlgorithm algorithm);

}  // namespace keykhan::totp
