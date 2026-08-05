using System;
using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    public struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
        // kernel32, minwinbase.h SECURITY_ATTRIBUTES; Pack 8 matches the x64 target of this solution.
    }
}
