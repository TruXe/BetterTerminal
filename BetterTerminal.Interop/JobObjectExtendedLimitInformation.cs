using System;
using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public IntPtr ProcessMemoryLimit;
        public IntPtr JobMemoryLimit;
        public IntPtr PeakProcessMemoryUsed;
        public IntPtr PeakJobMemoryUsed;
        // kernel32, winnt.h JOBOBJECT_EXTENDED_LIMIT_INFORMATION.
    }
}
