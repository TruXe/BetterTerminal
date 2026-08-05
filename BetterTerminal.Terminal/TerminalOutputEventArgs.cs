using System;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalOutputEventArgs : EventArgs
    {
        public TerminalOutputEventArgs(char[] buffer, int count)
        {
            Buffer = buffer;
            Count = count;
        }

        // The buffer is owned by the reader thread and is reused after the handler returns.
        public char[] Buffer { get; private set; }

        public int Count { get; private set; }
    }
}
