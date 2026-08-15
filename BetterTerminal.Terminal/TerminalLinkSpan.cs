namespace BetterTerminal.Terminal
{
    public struct TerminalLinkSpan
    {
        public int Start;
        public int End;
        public string Uri;

        public TerminalLinkSpan(int start, int end, string uri)
        {
            Start = start;
            End = end;
            Uri = uri;
        }
    }
}
