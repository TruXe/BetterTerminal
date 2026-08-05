using System;

namespace BetterTerminal.Interop
{
    // user32, winuser.h WNDENUMPROC callback contract for EnumWindows.
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
