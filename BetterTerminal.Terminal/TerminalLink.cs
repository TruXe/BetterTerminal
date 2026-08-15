using System.Collections.Generic;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalLink
    {
        public TerminalLink(int id, string uri, TerminalLinkOrigin origin)
        {
            Id = id;
            Uri = uri;
            Origin = origin;
            Ranges = new List<TerminalLinkRange>();
        }

        public int Id { get; private set; }

        public string Uri { get; private set; }

        public TerminalLinkOrigin Origin { get; private set; }

        public string Text { get; set; }

        public List<TerminalLinkRange> Ranges { get; private set; }
    }
}
