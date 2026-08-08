using System;
using System.Diagnostics;

namespace BetterTerminal.Service
{
    /// <summary>
    /// The one place the host writes to the Windows application log. A service has no window, so this
    /// log is where an operator - or a failed update - leaves a trace.
    /// </summary>
    internal static class ServiceLog
    {
        private const string Source = "BetterTerminal Host";

        public static void Write(string message)
        {
            try
            {
                if (!EventLog.SourceExists(Source))
                {
                    EventLog.CreateEventSource(Source, "Application");
                }

                EventLog.WriteEntry(Source, message, EventLogEntryType.Information);
            }
            catch (Exception)
            {
                // Logging is best effort: the host must run whether or not the log can be written.
            }
        }
    }
}
