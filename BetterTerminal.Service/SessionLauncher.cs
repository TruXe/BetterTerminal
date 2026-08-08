using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterTerminal.Service
{
    /// <summary>
    /// Runs a program in the interactive user's session on the service's behalf. The service is
    /// LocalSystem in session 0; to install the update into the user's own profile it must start the
    /// launcher with the user's token, or the launcher would unpack into the service account's
    /// profile instead. This is the supported route: take the active session's token and start the
    /// process with it.
    ///
    /// It is only used when nothing of ours is already running in that session - the running case is
    /// handled by the application over the pipe - so there is no window to disturb.
    /// </summary>
    internal static class SessionLauncher
    {
        private const uint NoActiveSession = 0xFFFFFFFF;
        private const uint MaximumAllowed = 0x02000000;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const uint CreateUnicodeEnvironment = 0x00000400;

        public static bool Run(string executable)
        {
            uint session = WTSGetActiveConsoleSessionId();
            if (session == NoActiveSession)
            {
                return false;
            }

            IntPtr userToken;
            if (!WTSQueryUserToken(session, out userToken))
            {
                ServiceLog.Write("Could not get the user token: error " + Marshal.GetLastWin32Error() + ".");
                return false;
            }

            IntPtr primaryToken = IntPtr.Zero;
            IntPtr environment = IntPtr.Zero;
            try
            {
                if (!DuplicateTokenEx(userToken, MaximumAllowed, IntPtr.Zero,
                    SecurityImpersonation, TokenPrimary, out primaryToken))
                {
                    ServiceLog.Write("Could not duplicate the user token: error " + Marshal.GetLastWin32Error() + ".");
                    return false;
                }

                // Without the user's environment block the child would inherit the service account's,
                // and %LOCALAPPDATA% would point at the wrong profile.
                CreateEnvironmentBlock(out environment, primaryToken, false);

                StartupInfo startup = new StartupInfo();
                startup.cb = Marshal.SizeOf(typeof(StartupInfo));
                startup.lpDesktop = "winsta0\\default";

                ProcessInformation process;
                StringBuilder commandLine = new StringBuilder("\"" + executable + "\"");

                bool started = CreateProcessAsUser(
                    primaryToken,
                    executable,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CreateUnicodeEnvironment,
                    environment,
                    Path.GetDirectoryName(executable),
                    ref startup,
                    out process);

                if (!started)
                {
                    ServiceLog.Write("Could not start the update in the user session: error " +
                        Marshal.GetLastWin32Error() + ".");
                    return false;
                }

                CloseHandle(process.hThread);
                CloseHandle(process.hProcess);
                return true;
            }
            finally
            {
                if (environment != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(environment);
                }

                if (primaryToken != IntPtr.Zero)
                {
                    CloseHandle(primaryToken);
                }

                CloseHandle(userToken);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateTokenEx(
            IntPtr existingToken,
            uint desiredAccess,
            IntPtr attributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr newToken);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr environment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessAsUser(
            IntPtr token,
            string applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
