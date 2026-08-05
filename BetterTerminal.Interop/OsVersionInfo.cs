using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct OsVersionInfo
    {
        public int OSVersionInfoSize;
        public int MajorVersion;
        public int MinorVersion;
        public int BuildNumber;
        public int PlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string CSDVersion;
        // ntdll, wdm.h RTL_OSVERSIONINFOW.
    }
}
