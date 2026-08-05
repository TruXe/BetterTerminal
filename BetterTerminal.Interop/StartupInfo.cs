using System;
using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    public struct StartupInfo
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
        // kernel32, processthreadsapi.h STARTUPINFOW; Pack 8 matches the x64 target of this solution.
    }
}
