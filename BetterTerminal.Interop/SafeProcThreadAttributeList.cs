using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace BetterTerminal.Interop
{
    [SecurityCritical]
    public sealed class SafeProcThreadAttributeList : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeProcThreadAttributeList(IntPtr list)
            : base(true)
        {
            SetHandle(list);
        }

        public static SafeProcThreadAttributeList Create(int attributeCount)
        {
            IntPtr size = IntPtr.Zero;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, attributeCount, 0, ref size);
            // First call always fails with ERROR_INSUFFICIENT_BUFFER and reports the size to allocate.

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!NativeMethods.InitializeProcThreadAttributeList(buffer, attributeCount, 0, ref size))
                {
                    Win32Error.Throw("InitializeProcThreadAttributeList");
                }
            }
            catch
            {
                Marshal.FreeHGlobal(buffer);
                throw;
            }

            return new SafeProcThreadAttributeList(buffer);
        }

        public void SetPseudoConsole(SafePseudoConsoleHandle pseudoConsole)
        {
            if (!NativeMethods.UpdateProcThreadAttribute(
                    handle,
                    0,
                    NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pseudoConsole.DangerousGetHandle(),
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                Win32Error.Throw("UpdateProcThreadAttribute");
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            NativeMethods.DeleteProcThreadAttributeList(handle);
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
