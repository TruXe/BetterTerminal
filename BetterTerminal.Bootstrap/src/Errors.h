#pragma once

#include <stdexcept>
#include <string>
#include <windows.h>

// Turns Windows and internal failures into exceptions carrying a readable message. Everything the
// bootstrapper does that can fail throws through here, so main has one place to report and exit.
namespace bt
{
    inline std::wstring FormatError(DWORD code)
    {
        LPWSTR buffer = nullptr;
        DWORD length = FormatMessageW(
            FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
            nullptr, code, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
            reinterpret_cast<LPWSTR>(&buffer), 0, nullptr);

        std::wstring text = (length > 0 && buffer != nullptr)
            ? std::wstring(buffer, length)
            : L"unknown error";

        if (buffer != nullptr)
        {
            LocalFree(buffer);
        }

        while (!text.empty() && (text.back() == L'\n' || text.back() == L'\r'))
        {
            text.pop_back();
        }

        return text;
    }

    inline std::string Narrow(const std::wstring& wide)
    {
        if (wide.empty())
        {
            return std::string();
        }

        int size = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(),
            static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);

        std::string narrow(static_cast<size_t>(size), '\0');
        WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.size()),
            &narrow[0], size, nullptr, nullptr);
        return narrow;
    }

    // The exception message is stored narrow, because std::exception::what returns char. It is read
    // back and widened for display in main.
    [[noreturn]] inline void ThrowWin(const std::wstring& what, DWORD code)
    {
        throw std::runtime_error(Narrow(what + L": " + FormatError(code)));
    }

    [[noreturn]] inline void ThrowMsg(const std::wstring& what)
    {
        throw std::runtime_error(Narrow(what));
    }
}
