#pragma once

#include <windows.h>

// A move-only owner for a Win32 HANDLE, so a process or thread handle is closed exactly once, on
// every path out - including an exception. There is deliberately no global handle anywhere; each is
// owned by the scope that opened it.
namespace bt
{
    class UniqueHandle
    {
    public:
        UniqueHandle() = default;

        explicit UniqueHandle(HANDLE handle) : handle_(handle)
        {
        }

        ~UniqueHandle()
        {
            Reset();
        }

        UniqueHandle(const UniqueHandle&) = delete;
        UniqueHandle& operator=(const UniqueHandle&) = delete;

        UniqueHandle(UniqueHandle&& other) noexcept : handle_(other.handle_)
        {
            other.handle_ = nullptr;
        }

        UniqueHandle& operator=(UniqueHandle&& other) noexcept
        {
            if (this != &other)
            {
                Reset();
                handle_ = other.handle_;
                other.handle_ = nullptr;
            }

            return *this;
        }

        HANDLE Get() const
        {
            return handle_;
        }

        void Reset()
        {
            if (handle_ != nullptr && handle_ != INVALID_HANDLE_VALUE)
            {
                CloseHandle(handle_);
            }

            handle_ = nullptr;
        }

        explicit operator bool() const
        {
            return handle_ != nullptr && handle_ != INVALID_HANDLE_VALUE;
        }

    private:
        HANDLE handle_ = nullptr;
    };
}
