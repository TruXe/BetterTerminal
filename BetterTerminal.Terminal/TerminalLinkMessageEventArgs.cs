using System;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalLinkMessageEventArgs : EventArgs
    {
        public TerminalLinkMessageEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}
