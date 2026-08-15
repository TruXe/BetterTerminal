using System;

namespace BetterTerminal.Terminal
{
    [Flags]
    public enum CellFlags
    {
        None = 0,
        Bold = 1,
        Dim = 2,
        Italic = 4,
        Underline = 8,
        Inverse = 16,
        Hidden = 32,
        LineWrapped = 64,
        WideTrailing = 128
    }
}
