using System;
using System.Diagnostics;
using System.ServiceProcess;

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
        }

        protected override void OnStop()
        {
            Log("Service stopped.");
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
