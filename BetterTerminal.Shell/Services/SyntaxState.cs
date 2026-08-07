namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// What was still open at the end of the previous line. A line cannot be coloured on its own -
    /// a block comment or an element opened three lines up decides how this one reads.
    /// </summary>
    public enum SyntaxState
    {
        Normal,
        BlockComment,
        InsideTag
    }
}
