using System;

namespace BetterTerminal.Shell.Views
{
    public sealed class PaletteInputEventArgs : EventArgs
    {
        public PaletteInputEventArgs(string line)
        {
            Line = line;
        }

        public string Line { get; private set; }
    }
}
