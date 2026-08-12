using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BetterTerminal.Notifications;
using BetterTerminal.Updating;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// The application's own update watch. It checks the release feed directly - at start, then now
    /// and then - so a new version is announced at once instead of only when the service happens to
    /// poll, which is what left an earlier build silent. It also records which version is installed
    /// and listens on the service's pipe, so a build the service stages while the application runs is
    /// shown too. Either way the notice appears and applying is the user's one click, so a live
    /// session is never pulled out from under them.
    /// </summary>
    public sealed class UpdateClient
    {
        private const int ConnectTimeoutMs = 3000;
        private const int ReconnectDelayMs = 15000;
        private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        private readonly Dispatcher _dispatcher;
        private Thread _thread;
        private Timer _selfCheck;
        private volatile bool _running;
        private bool _presenting;
        private volatile bool _checking;
        private Version _shown;

        public UpdateClient(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            UpdateShared.WriteInstalled(SelfInstall.RunningVersion, SelfInstall.InstalledExecutable);

            _running = true;
            _thread = new Thread(Listen) { IsBackground = true, Name = "update-client" };
            _thread.Start();

            // Ask the release feed ourselves, soon after the window is up and then hourly, so the
            // notice does not depend on the service being installed or on its next poll.
            _selfCheck = new Timer(delegate { SelfCheck(); }, null, FirstCheckDelay, CheckInterval);
        }

        public void Stop()
        {
            _running = false;

            if (_selfCheck != null)
            {
                _selfCheck.Dispose();
                _selfCheck = null;
            }
        }

        /// <summary>
        /// The check the user asked for, from the caption strip. Same probe as the timer's, with one
        /// difference that matters: it always answers. A silent button is indistinguishable from a
        /// broken one, so "you are up to date" and "the check could not be made" are notices too, and
        /// a version already announced is announced again rather than swallowed as a repeat.
        /// </summary>
        public void CheckNow()
        {
            if (_checking)
            {
                return;
            }

            _checking = true;
            ThreadPool.QueueUserWorkItem(delegate { RunRequestedCheck(); });
        }

        private void RunRequestedCheck()
        {
            UpdateProbeResult result = null;
            bool reached = true;

            try
            {
                result = UpdateProbe.Check();
            }
            catch (Exception)
            {
                // No network, no release feed, no answer. The user is told that much and nothing here
                // is allowed to take the application down.
                reached = false;
            }

            _dispatcher.BeginInvoke(new Action(delegate { PresentRequested(result, reached); }));
        }

        private void PresentRequested(UpdateProbeResult result, bool reached)
        {
            _checking = false;

            if (result != null)
            {
                // Asking again is a request to be shown the answer, so the once-only guard is lifted.
                _shown = null;
                Present(result.Version, result.Launcher);
                return;
            }

            ToastNotification toast = new ToastNotification
            {
                AppName = "BetterTerminal",
                Title = reached ? "No update available" : "Update check failed",
                Message = reached
                    ? "Version " + UpdateShared.NormalizedString(UpdateShared.Normalize(SelfInstall.RunningVersion)) +
                        " is the newest there is."
                    : "The list of releases could not be reached. Nothing was changed; try again later.",
                Duration = TimeSpan.FromSeconds(8)
            };

            toast.Show();
        }

        private void SelfCheck()
        {
            try
            {
                UpdateProbeResult result = UpdateProbe.Check();
                if (result != null)
                {
                    _dispatcher.BeginInvoke(new Action(delegate { Present(result.Version, result.Launcher); }));
                }
            }
            catch (Exception)
            {
                // A background update check must never take the application down; a transient network
                // failure just means no notice this round, and the next check tries again.
            }
        }

        private void Listen()
        {
            while (_running)
            {
                try
                {
                    using (NamedPipeClientStream client =
                        new NamedPipeClientStream(".", UpdateShared.PipeName, PipeDirection.InOut))
                    {
                        client.Connect(ConnectTimeoutMs);
                        using (StreamReader reader = new StreamReader(client))
                        {
                            string line;
                            while (_running && (line = reader.ReadLine()) != null)
                            {
                                Handle(line);
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // The service is not installed or not running yet. The update feature depends on
                    // it, so there is nothing to do but wait and try again.
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                if (_running)
                {
                    Thread.Sleep(ReconnectDelayMs);
                }
            }
        }

        private void Handle(string line)
        {
            if (!line.StartsWith(UpdateShared.UpdateMessagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Version version;
            if (!Version.TryParse(line.Substring(UpdateShared.UpdateMessagePrefix.Length).Trim(), out version))
            {
                return;
            }

            Version normalized = UpdateShared.Normalize(version);

            // The service staged this into the machine-wide folder; that is where its launcher is.
            string launcher = UpdateShared.ReadStagedLauncher(normalized);
            _dispatcher.BeginInvoke(new Action(delegate { Present(normalized, launcher); }));
        }

        private void Present(Version version, string launcher)
        {
            // Both the pipe and the self-check can report the same version; show it once so a second
            // report - a reconnect, or a self-check after the service already told us - does not
            // reopen the toast.
            if (_presenting || launcher == null || (_shown != null && _shown == version))
            {
                return;
            }

            _presenting = true;
            _shown = version;

            // The same notification the service raises when the application is closed, shown here in
            // its own running session. "Restart now" applies the staged build and relaunches; leaving
            // it be keeps the live session untouched and applies on the next start instead.
            ToastNotification toast = new ToastNotification
            {
                AppName = "BetterTerminal",
                Title = "Update ready",
                Message = "Version " + UpdateShared.NormalizedString(version) +
                    " is ready. It installs when you restart BetterTerminal.",
                Duration = TimeSpan.FromSeconds(12)
            };

            toast.Actions.Add(new ToastAction("Restart now", delegate
            {
                if (UpdateApply.Launch(launcher))
                {
                    Application.Current.Shutdown();
                }
            }));

            toast.Closed += delegate { _presenting = false; };
            toast.Show();
        }
    }
}
