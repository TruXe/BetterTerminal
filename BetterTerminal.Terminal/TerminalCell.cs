namespace BetterTerminal.Terminal
{
    public struct TerminalCell
    {
        // Zero means "use the theme default", which is safe because every real colour is opaque ARGB.
        public int Foreground;
        public int Background;
        public char Character;
        public ushort LinkId;
        public CellFlags Flags;

        public bool SameAttributes(TerminalCell other)
        {
            return Foreground == other.Foreground
                && Background == other.Background
                && Flags == other.Flags;
        }
    }
}
