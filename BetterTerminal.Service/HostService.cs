using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using BetterTerminal.Updating;

namespace BetterTerminal.Service
{
    /// <summary>
    /// The background host that stands for BetterTerminal's helper components as a service. It runs
    /// with no window - a service has no desktop - and its whole visible life is its entry in the
    /// service list and the lines it writes to the Windows application log when it starts and stops.
    ///
    /// It does not run the banner or the wizard itself: those are a session's own programs and need
    /// a real console, which a service does not have. What it provides is a registered, always-on
    /// presence that records which helper programs are staged beside it, so the components are
    /// installed and accounted for as a service the way the operator asked.
    /// </summary>
    public sealed class HostService : ServiceBase
    {
        internal const string Name = "BetterTerminalHost";
        internal const string Display = "BetterTerminal Host";
        internal const string Description =
            "Background host for BetterTerminal helper components. Runs without a window.";

        private const string LogSource = "BetterTerminal Host";

        private UpdateSignal _signal;
        private Timer _poll;

        public HostService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            CanPauseAndContinue = false;
            AutoLog = false;
        }

        protected override void OnStart(string[] args)
        {
            Log("Service started. " + HelperInventory.Describe());
            StartUpdates();
        }

        protected override void OnStop()
        {
            StopUpdates();
            Log("Service stopped.");
        }

        private void StartUpdates()
        {
            _signal = new UpdateSignal();

            // A version already staged from a previous run is offered to the first client that
            // connects, before the first poll has had a chance to run.
            Version staged = UpdateShared.ReadStagedVersion();
            if (staged != null)
            {
                _signal.SetAvailable(staged);
            }

            _signal.Start();
            _poll = new Timer(Poll, null, UpdateShared.InitialPollDelay, UpdateShared.PollInterval);
        }

        private void StopUpdates()
        {
            if (_poll != null)
            {
                _poll.Dispose();
                _poll = null;
            }

            if (_signal != null)
            {
                _signal.Dispose();
                _signal = null;
            }
        }

        private void Poll(object state)
        {
            try
            {
                Version found = UpdateCheck.Run();
                if (found != null && _signal != null)
                {
                    _signal.SetAvailable(found);
                    Log("Staged update " + UpdateShared.NormalizedString(found) + ".");
                }
            }
            catch (Exception error)
            {
                // A failed check must not take the service down; the next poll tries again.
                Log("Update check failed: " + error.Message);
            }
        }

        protected override void OnShutdown()
        {
            Log("Service stopping for system shutdown.");
        }

        /// <summary>Runs the host in the foreground for a quick check from an elevated prompt.</summary>
        internal void RunConsole()
        {
            OnStart(new string[0]);
            Console.WriteLine(Display + " running. " + HelperInventory.Describe());
            Console.WriteLine("Press Enter to stop.");
            Console.ReadLine();
            OnStop();
        }

        private static void Log(string message)
        {
            try
            {
                if (!EventLog.SourceExists(LogSource))
                {
                    EventLog.CreateEventSource(LogSource, "Application");
                }

                EventLog.WriteEntry(LogSource, message, EventLogEntryType.Information);
            }
            catch (Exception)
            {
                // Logging is best effort: the host must run whether or not the log can be written.
            }
        }
    }
}
