using System;
using System.IO;
using BetterTerminal.Updating;

namespace BetterTerminal.Service
{
    /// <summary>
    /// Raises the update notice in the interactive session on the service's behalf. The service is in
    /// session 0 and cannot draw a window itself, so it starts the application in the user's session
    /// with the switch that makes it load the notification library and show only that notice. The
    /// library's window is the application's own, so it appears whether or not the account has Windows
    /// notifications turned on - unlike a system toast, which the notification centre silently holds
    /// back when they are off.
    /// </summary>
    internal static class ToastNotice
    {
        public static bool Show(Version version)
        {
            // The application records its own installed path where both processes can read it; the
            // service cannot expand the user's profile path on its own.
            string executable = UpdateShared.ReadInstalledExecutable();
            if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
            {
                return false;
            }

            return SessionLauncher.Run(executable, UpdateShared.UpdateNotifyArguments(version));
        }
    }
}
