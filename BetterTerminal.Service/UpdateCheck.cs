using System;
using System.Reflection;
using BetterTerminal.Updating;

namespace BetterTerminal.Service
{
    /// <summary>
    /// One update check, start to finish: work out what is installed, ask what the latest release is,
    /// and if it is newer, stage it and record it. Returns the staged version, or null when there is
    /// nothing newer or the download did not arrive intact. Calling it again after a version is
    /// already staged does no work and does not download it a second time.
    /// </summary>
    internal static class UpdateCheck
    {
        public static Version Run()
        {
            Version installed = InstalledVersion();

            ReleaseInfo release = ReleaseFeed.Latest();
            if (release == null || !UpdateShared.IsNewer(release.Version, installed))
            {
                return null;
            }

            string launcher = UpdateDownloader.Stage(release, installed, UpdateShared.StagingDirectory);
            if (launcher == null)
            {
                return null;
            }

            UpdateShared.WriteStaged(release.Version, launcher);
            return UpdateShared.Normalize(release.Version);
        }

        /// <summary>Whatever was last recorded as installed, taken together with the version this
        /// service shipped as. The service is part of the same release, so its own version is a floor
        /// for the installed version and stops a check from offering an "upgrade" to a build that is
        /// already here when the application has not yet written its record.</summary>
        private static Version InstalledVersion()
        {
            Version recorded = UpdateShared.ReadInstalledVersion();
            Version own = UpdateShared.Normalize(Assembly.GetExecutingAssembly().GetName().Version);

            if (recorded == null)
            {
                return own;
            }

            return UpdateShared.Normalize(recorded) >= own ? recorded : own;
        }
    }
}
