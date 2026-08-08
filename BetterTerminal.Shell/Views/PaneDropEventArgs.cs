using System;

namespace BetterTerminal.Shell.Views
{
    public sealed class PaneDropEventArgs : EventArgs
    {
        public PaneDropEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}
