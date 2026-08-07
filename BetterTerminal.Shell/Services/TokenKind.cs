namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// What one run of characters is. Deliberately few: the eight that carry meaning across every
    /// language the viewer knows, each with one brush in every theme tier.
    /// </summary>
    public enum TokenKind
    {
        Plain,
        Comment,
        String,
        Number,
        Keyword,
        Tag,
        Attribute,
        Property,
        Operator
    }
}
