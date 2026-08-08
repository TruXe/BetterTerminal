#include <exception>
#include <string>
#include <windows.h>
#include <shellapi.h>

#include "Bootstrapper.h"
#include "Log.h"
#include "Errors.h"

namespace
{
    // True when the launcher's own arguments begin with "--wait <pid>". The updater passes this so
    // the launcher can replace a running copy: the old application must exit before the payload is
    // unpacked over the install folder, or the files it holds open cannot be written.
    bool HasWaitArgument(int count, LPWSTR* argv)
    {
        return count >= 3 && std::wstring(argv[1]) == L"--wait";
    }

    // Everything the launcher itself was given after its own name, rebuilt so an argument with a
    // space keeps its quoting when it reaches the application. This is what lets the launcher stand
    // in for the application - "launcher --project C:\path" reaches the app as "--project C:\path". A
    // leading "--wait <pid>" is the launcher's own and is dropped, so the application never sees it.
    std::wstring ForwardedArguments()
    {
        int count = 0;
        LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &count);
        if (argv == nullptr)
        {
            return std::wstring();
        }

        int start = HasWaitArgument(count, argv) ? 3 : 1;

        std::wstring result;
        for (int index = start; index < count; ++index)
        {
            std::wstring argument = argv[index];
            if (argument.find(L' ') != std::wstring::npos)
            {
                argument = L"\"" + argument + L"\"";
            }

            if (!result.empty())
            {
                result += L" ";
            }

            result += argument;
        }

        LocalFree(argv);
        return result;
    }

    // Waits for the process named by a leading "--wait <pid>" to exit before returning, bounded so a
    // process that never exits cannot hang the update. Does nothing when the argument is absent.
    void WaitForCallerIfAsked()
    {
        const DWORD MaximumWaitMs = 20000;

        int count = 0;
        LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &count);
        if (argv == nullptr)
        {
            return;
        }

        if (HasWaitArgument(count, argv))
        {
            DWORD pid = static_cast<DWORD>(_wtoi(argv[2]));
            if (pid != 0)
            {
                HANDLE process = OpenProcess(SYNCHRONIZE, FALSE, pid);
                if (process != nullptr)
                {
                    WaitForSingleObject(process, MaximumWaitMs);
                    CloseHandle(process);
                }
            }
        }

        LocalFree(argv);
    }
}

// A windowed subsystem entry point, so starting the launcher flashes no console of its own; the
// application it starts brings its own window. Any failure is shown once, in a message box, and
// turned into a non-zero exit code - never a silent stall.
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    try
    {
        WaitForCallerIfAsked();
        return bt::Bootstrapper(instance).Run(ForwardedArguments());
    }
    catch (const std::exception& error)
    {
        // error.what() is UTF-8; widen it for display.
        int size = MultiByteToWideChar(CP_UTF8, 0, error.what(), -1, nullptr, 0);
        std::wstring wide(static_cast<size_t>(size > 0 ? size - 1 : 0), L'\0');
        if (size > 1)
        {
            MultiByteToWideChar(CP_UTF8, 0, error.what(), -1, &wide[0], size);
        }

        bt::Log(L"Fatal: " + wide);
        MessageBoxW(nullptr, wide.c_str(), L"BetterTerminal", MB_ICONERROR | MB_OK);
        return 1;
    }
}
