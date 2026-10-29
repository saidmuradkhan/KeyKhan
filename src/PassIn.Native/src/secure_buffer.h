#pragma once

#include <windows.h>

#include <cstddef>
#include <cstdint>
#include <new>
#include <span>
#include <utility>

namespace passin {

// Page-backed memory that is pinned in RAM (kept out of the page file) and wiped on release.
class SecureBuffer {
public:
    explicit SecureBuffer(std::size_t size) : size_(size) {
        if (size_ == 0) {
            return;
        }
        data_ = static_cast<std::uint8_t*>(VirtualAlloc(nullptr, size_, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE));
        if (data_ == nullptr) {
            throw std::bad_alloc();
        }
        // Locking can fail when the working-set quota is exhausted; the buffer is still wiped on release.
        locked_ = VirtualLock(data_, size_) != FALSE;
    }

    ~SecureBuffer() { release(); }

    SecureBuffer(const SecureBuffer&) = delete;
    SecureBuffer& operator=(const SecureBuffer&) = delete;

    SecureBuffer(SecureBuffer&& other) noexcept
        : data_(std::exchange(other.data_, nullptr)),
          size_(std::exchange(other.size_, 0)),
          locked_(std::exchange(other.locked_, false)) {}

    SecureBuffer& operator=(SecureBuffer&& other) noexcept {
        if (this != &other) {
            release();
            data_ = std::exchange(other.data_, nullptr);
            size_ = std::exchange(other.size_, 0);
            locked_ = std::exchange(other.locked_, false);
        }
        return *this;
    }

    [[nodiscard]] std::uint8_t* data() noexcept { return data_; }
    [[nodiscard]] std::size_t size() const noexcept { return size_; }
    [[nodiscard]] std::span<std::uint8_t> span() noexcept { return {data_, size_}; }
    [[nodiscard]] std::span<const std::uint8_t> span() const noexcept { return {data_, size_}; }

private:
    void release() noexcept {
        if (data_ == nullptr) {
            return;
        }
        SecureZeroMemory(data_, size_);
        if (locked_) {
            VirtualUnlock(data_, size_);
        }
        VirtualFree(data_, 0, MEM_RELEASE);
        data_ = nullptr;
        size_ = 0;
        locked_ = false;
    }

    std::uint8_t* data_ = nullptr;
    std::size_t size_ = 0;
    bool locked_ = false;
};

}  // namespace passin
