#pragma once

#include <string>
#include <windows.h>

// A tiny, quiet logging layer. In a Debug build it writes to the debugger's output; in Release it
// compiles to nothing, so a shipped launcher says nothing at all. Errors that matter reach the user
// through a message box in main, not through this.
namespace bt
{
#ifdef _DEBUG
    inline void Log(const std::wstring& message)
    {
        OutputDebugStringW((L"[BetterTerminal.Bootstrap] " + message + L"\n").c_str());
    }
#else
    inline void Log(const std::wstring&)
    {
    }
#endif
}
