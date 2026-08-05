using System;
using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public int LimitFlags;
        public IntPtr MinimumWorkingSetSize;
        public IntPtr MaximumWorkingSetSize;
        public int ActiveProcessLimit;
        public IntPtr Affinity;
        public int PriorityClass;
        public int SchedulingClass;
        // kernel32, winnt.h JOBOBJECT_BASIC_LIMIT_INFORMATION.
    }
}
