using System;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalExitEventArgs : EventArgs
    {
        public TerminalExitEventArgs(int exitCode, string reason)
        {
            ExitCode = exitCode;
            Reason = reason;
        }

        public int ExitCode { get; private set; }

        public string Reason { get; private set; }
    }
}
