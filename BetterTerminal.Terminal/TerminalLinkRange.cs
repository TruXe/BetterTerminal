namespace BetterTerminal.Terminal
{
    public struct TerminalLinkRange
    {
        public int Line;
        public int Start;
        public int End;

        public TerminalLinkRange(int line, int start, int end)
        {
            Line = line;
            Start = start;
            End = end;
        }

        public bool Covers(int line, int column)
        {
            return line == Line && column >= Start && column < End;
        }
    }
}
