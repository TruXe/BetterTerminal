using System;
using System.Runtime.InteropServices;

namespace BetterTerminal.Service
{
    /// <summary>
    /// How the service shows a message on the user's desktop when nothing of ours is running there.
    /// A service is in session 0 and cannot draw a window into the interactive session, but it can
    /// ask the session manager to put a message box in front of the logged-on user - which is what
    /// makes a notice possible with no application open. It is the plain system message box, not the
    /// application's own styled notice; that one needs a process in the user's session and is shown
    /// by the application when it is running.
    /// </summary>
    internal static class SessionNotice
    {
        private const int MbIconInformation = 0x40;
        private const uint NoActiveSession = 0xFFFFFFFF;

        // Auto-dismiss so a message no one is at does not sit on the desktop forever.
        private const int TimeoutSeconds = 20;

        public static bool Show(string title, string message)
        {
            uint session = WTSGetActiveConsoleSessionId();
            if (session == NoActiveSession)
            {
                return false;
            }

            int response;
            bool shown = WTSSendMessageW(
                IntPtr.Zero,
                session,
                title,
                title.Length * 2,
                message,
                message.Length * 2,
                MbIconInformation,
                TimeoutSeconds,
                out response,
                false);

            if (!shown)
            {
                ServiceLog.Write("Could not show the update notice: error " + Marshal.GetLastWin32Error() + ".");
            }

            return shown;
        }

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSSendMessageW(
            IntPtr hServer,
            uint sessionId,
            string pTitle,
            int titleLength,
            string pMessage,
            int messageLength,
            int style,
            int timeoutSeconds,
            out int response,
            [MarshalAs(UnmanagedType.Bool)] bool wait);
    }
}
