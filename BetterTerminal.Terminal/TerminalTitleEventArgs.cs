using System;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalTitleEventArgs : EventArgs
    {
        public TerminalTitleEventArgs(string title)
        {
            Title = title;
        }

        public string Title { get; private set; }
    }
}
