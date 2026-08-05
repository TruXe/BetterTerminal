using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace BetterTerminal.Interop
{
    [SecurityCritical]
    public sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeKernelHandle()
            : base(true)
        {
        }

        public SafeKernelHandle(IntPtr existingHandle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(existingHandle);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
        // kernel32, handleapi.h
    }
}
