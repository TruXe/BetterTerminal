#pragma once

#include <filesystem>
#include <string>
#include <vector>
#include <windows.h>

#include "Handle.h"
#include "Log.h"
#include "Errors.h"

namespace bt
{
    // Starts the extracted application and waits for it to finish. The working directory is set to
    // the extraction folder, so the application sees itself exactly as an installed copy would - its
    // own first-run install then copies itself out to the user profile, which is why the temporary
    // copy can be removed afterwards without breaking the "beterm" command.
    class ProcessLauncher
    {
    public:
        static DWORD Run(const std::filesystem::path& executable,
            const std::wstring& arguments, const std::filesystem::path& workingDirectory)
        {
            std::wstring commandLine = L"\"" + executable.wstring() + L"\"";
            if (!arguments.empty())
            {
                commandLine += L" " + arguments;
            }

            // CreateProcessW may write into the command-line buffer, so it must be mutable.
            std::vector<wchar_t> buffer(commandLine.begin(), commandLine.end());
            buffer.push_back(L'\0');

            STARTUPINFOW startup{};
            startup.cb = sizeof(startup);
            PROCESS_INFORMATION process{};

            BOOL started = CreateProcessW(
                executable.wstring().c_str(),
                buffer.data(),
                nullptr,
                nullptr,
                FALSE,
                0,
                nullptr,
                workingDirectory.wstring().c_str(),
                &startup,
                &process);

            if (!started)
            {
                ThrowWin(L"The application could not be started", GetLastError());
            }

            UniqueHandle processHandle(process.hProcess);
            UniqueHandle threadHandle(process.hThread);

            Log(L"Launched " + executable.wstring());
            WaitForSingleObject(processHandle.Get(), INFINITE);

            DWORD exitCode = 0;
            if (!GetExitCodeProcess(processHandle.Get(), &exitCode))
            {
                exitCode = 0;
            }

            return exitCode;
        }
    };
}
