#pragma once

#include <filesystem>
#include <string>
#include <objbase.h>
#include <windows.h>

#include "Log.h"
#include "Errors.h"

#pragma comment(lib, "ole32.lib")

namespace bt
{
    // Owns a unique, private working directory under %TEMP%\BetterTerminal\{GUID}. Creating it is
    // the constructor; removing it, with everything written into it, is the destructor - so the
    // cleanup runs on every path out of the scope, including an exception during extraction or
    // launch. This is the RAII the task asks for: the directory's lifetime is this object's.
    class TempDirectory
    {
    public:
        TempDirectory()
        {
            std::error_code ec;
            std::filesystem::path base = std::filesystem::temp_directory_path(ec) / L"BetterTerminal";
            if (ec)
            {
                ThrowMsg(L"The temporary folder location could not be determined");
            }

            path_ = base / NewGuid();

            std::filesystem::create_directories(path_, ec);
            if (ec)
            {
                ThrowMsg(L"The temporary working directory could not be created");
            }

            Log(L"Created working directory " + path_.wstring());
        }

        ~TempDirectory()
        {
            Remove();
        }

        TempDirectory(const TempDirectory&) = delete;
        TempDirectory& operator=(const TempDirectory&) = delete;

        const std::filesystem::path& Path() const
        {
            return path_;
        }

    private:
        static std::wstring NewGuid()
        {
            GUID guid;
            if (CoCreateGuid(&guid) == S_OK)
            {
                wchar_t buffer[64] = { 0 };
                if (StringFromGUID2(guid, buffer, 64) > 0)
                {
                    std::wstring text = buffer;
                    if (text.size() >= 2 && text.front() == L'{')
                    {
                        text = text.substr(1, text.size() - 2);
                    }

                    return text;
                }
            }

            return std::to_wstring(GetTickCount64());
        }

        void Remove()
        {
            if (path_.empty())
            {
                return;
            }

            std::error_code ec;
            std::filesystem::remove_all(path_, ec);
            if (ec)
            {
                Log(L"Cleanup could not remove everything under " + path_.wstring());
            }
            else
            {
                Log(L"Removed working directory " + path_.wstring());
            }
        }

        std::filesystem::path path_;
    };
}
