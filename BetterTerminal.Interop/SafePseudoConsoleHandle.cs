using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace BetterTerminal.Interop
{
    [SecurityCritical]
    public sealed class SafePseudoConsoleHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafePseudoConsoleHandle()
            : base(true)
        {
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            ClosePseudoConsole(handle);
            return true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private static extern void ClosePseudoConsole(IntPtr hPC);
        // kernel32, consoleapi.h, Windows 10 17763+; void return, no failure path.
    }
}
