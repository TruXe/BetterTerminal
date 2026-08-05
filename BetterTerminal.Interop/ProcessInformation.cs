using System;
using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
        // kernel32, processthreadsapi.h PROCESS_INFORMATION.
    }
}
