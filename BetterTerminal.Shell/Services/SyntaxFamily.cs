namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// How a file has to be read to be coloured. Most languages differ only in their comment
    /// markers and their keywords, and those all share <see cref="Generic"/>; the two that carry
    /// structure in the text itself get a reader of their own.
    /// </summary>
    public enum SyntaxFamily
    {
        Generic,
        Json,
        Markup
    }
}
