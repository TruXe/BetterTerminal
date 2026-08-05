using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    public static class Win32Error
    {
        public static void Throw(string api)
        {
            int lastError = Marshal.GetLastWin32Error();
            throw new Win32Exception(lastError, api + " failed with Win32 error " + lastError + ".");
        }

        public static void ThrowIfFailed(int hresult, string api)
        {
            if (hresult != 0)
            {
                throw new Win32Exception(hresult, api + " failed with HRESULT 0x" + hresult.ToString("X8") + ".");
            }
        }
    }
}
