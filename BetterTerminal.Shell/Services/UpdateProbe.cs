using System;
using BetterTerminal.Updating;

namespace BetterTerminal.Shell.Services
{
    internal sealed class UpdateProbeResult
    {
        public UpdateProbeResult(Version version, string launcher)
        {
            Version = version;
            Launcher = launcher;
        }

        public Version Version { get; private set; }

        public string Launcher { get; private set; }
    }

    /// <summary>
    /// The application's own update check. The service checks in the background too, but only on its
    /// timer and only when it is installed and running; asking the release feed directly at start is
    /// what lets a new version be announced at once. The download is staged into the user's profile,
    /// so the notice's Restart is instant and needs nothing from the service.
    /// </summary>
    internal static class UpdateProbe
    {
        public static UpdateProbeResult Check()
        {
            Version running = UpdateShared.Normalize(SelfInstall.RunningVersion);

            ReleaseInfo release = ReleaseFeed.Latest();
            if (release == null || !UpdateShared.IsNewer(release.Version, running))
            {
                return null;
            }

            string launcher = UpdateDownloader.Stage(release, running, UpdateShared.AppStagingDirectory);
            return launcher == null
                ? null
                : new UpdateProbeResult(UpdateShared.Normalize(release.Version), launcher);
        }
    }
}
