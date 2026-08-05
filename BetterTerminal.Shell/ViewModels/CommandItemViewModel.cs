using System;

namespace BetterTerminal.Shell.ViewModels
{
    public sealed class CommandItemViewModel
    {
        public string Name { get; set; }

        public string Group { get; set; }

        public string Glyph { get; set; }

        public string KeysDisplay { get; set; }

        public string Source { get; set; }

        /// <summary>
        /// What running this entry does. Null entries are display-only (design-time data).
        /// </summary>
        public Action Run { get; set; }
    }
}
