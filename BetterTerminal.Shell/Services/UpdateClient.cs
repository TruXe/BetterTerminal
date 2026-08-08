using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BetterTerminal.Shell.Views;
using BetterTerminal.Updating;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// The application's end of the update pipe. It records which version is installed so the service
    /// knows what to compare against, then listens for the service to say a newer build is staged and
    /// shows the notification when it does. The service does the checking and downloading; this side
    /// only records, listens, and presents.
    ///
    /// A build staged while the application was closed is handled elsewhere, at startup, by
    /// <see cref="UpdateApply.TryApplyOnStartup"/>. This client covers the running case: the toast
    /// appears, and applying is the user's one click, so live sessions are never pulled out from
    /// under them.
    /// </summary>
    public sealed class UpdateClient
    {
        private const int ConnectTimeoutMs = 3000;
        private const int ReconnectDelayMs = 15000;

        private readonly Dispatcher _dispatcher;
        private Thread _thread;
        private volatile bool _running;
        private bool _presenting;
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
        }

        public void Stop()
        {
            _running = false;
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
            _dispatcher.BeginInvoke(new Action(delegate { Present(normalized); }));
        }

        private void Present(Version version)
        {
            // Reconnecting re-sends the same news; show it once per version so a dropped pipe does not
            // reopen the toast every time it comes back.
            if (_presenting || (_shown != null && _shown == version))
            {
                return;
            }

            string launcher = UpdateShared.ReadStagedLauncher(version);
            if (launcher == null)
            {
                return;
            }

            _presenting = true;
            _shown = version;

            UpdateToastWindow toast = new UpdateToastWindow(version, delegate
            {
                if (UpdateApply.Launch(launcher))
                {
                    Application.Current.Shutdown();
                }
            });

            toast.Closed += delegate { _presenting = false; };
            toast.Show();
        }
    }
}
