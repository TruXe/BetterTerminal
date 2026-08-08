using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using BetterTerminal.Updating;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Applies a staged update by running the downloaded launcher. The launcher unpacks the newer
    /// build over the install folder and then runs it; it cannot replace files this process is
    /// holding open, so it is told to wait for this process to exit first.
    /// </summary>
    internal static class UpdateApply
    {
        /// <summary>
        /// Run at the very start, before any window or session exists. If the service staged a newer
        /// build while the application was closed, this launches it and returns true so the caller
        /// exits at once and the upgrade finishes with nothing of the old version in the way. Returns
        /// false in the ordinary case, and the application starts normally.
        /// </summary>
        public static bool TryApplyOnStartup()
        {
            Version staged = UpdateShared.ReadStagedVersion();
            if (staged == null)
            {
                return false;
            }

            if (!UpdateShared.IsNewer(staged, UpdateShared.Normalize(SelfInstall.RunningVersion)))
            {
                // The staged build is this one or older: it has already been applied. Clear the
                // record so the next start does not consider it again.
                UpdateShared.ClearStaged();
                return false;
            }

            string launcher = UpdateShared.ReadStagedLauncher(staged);
            return launcher != null && Launch(launcher);
        }

        public static bool Launch(string launcher)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(
                    launcher, "--wait " + Process.GetCurrentProcess().Id);
                start.UseShellExecute = false;
                start.WorkingDirectory = Path.GetDirectoryName(launcher);

                Process.Start(start);
                return true;
            }
            catch (Win32Exception)
            {
                // The launcher could not be started; the application keeps the version it has.
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
