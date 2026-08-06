#pragma once

#include <filesystem>
#include <string>
#include <windows.h>

#include "Log.h"
#include "PayloadArchive.h"
#include "PayloadExtractor.h"
#include "ProcessLauncher.h"
#include "ResourceManager.h"
#include "TempDirectory.h"
#include "Errors.h"
#include "../resource.h"

namespace bt
{
    // Ties the pieces together in the order the task lays out: read the embedded payload, unpack it
    // into a private temporary directory, start the application there, wait for it, and return its
    // exit code as our own. The temporary directory removes itself when this function returns, on
    // success or on an exception, because it is a local with a destructor.
    class Bootstrapper
    {
    public:
        // The one C# program the launcher starts. The rest of the payload is the assemblies and
        // files it needs beside it.
        static constexpr const wchar_t* ApplicationExecutable = L"BetterTerminal.exe";

        explicit Bootstrapper(HMODULE module) : module_(module)
        {
        }

        int Run(const std::wstring& forwardedArguments) const
        {
            ResourceManager resources(module_);
            ByteView blob = resources.Load(IDR_PAYLOAD);

            PayloadArchive archive(blob);

            TempDirectory temp;
            PayloadExtractor::Extract(archive, temp.Path());

            std::filesystem::path executable = temp.Path() / ApplicationExecutable;
            if (!std::filesystem::exists(executable))
            {
                ThrowMsg(L"The application executable is missing from the payload");
            }

            DWORD exitCode = ProcessLauncher::Run(executable, forwardedArguments, temp.Path());
            Log(L"Application exited with code " + std::to_wstring(exitCode));
            return static_cast<int>(exitCode);
        }

    private:
        HMODULE module_;
    };
}
