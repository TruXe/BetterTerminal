namespace BetterTerminal.Wrap
{
    /// <summary>A colour as the three components a true-colour escape sequence needs.</summary>
    public struct TerminalColor
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public TerminalColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>
    /// The same colours the application's dark theme uses, transcribed from
    /// BetterTerminal.Shell\Themes\Primitives.xaml so the two look like one product. This is the
    /// only place in this program where a colour is written down.
    /// </summary>
    public static class Palette
    {
        public static readonly TerminalColor Window = new TerminalColor(0x0E, 0x0F, 0x11);
        public static readonly TerminalColor Chrome = new TerminalColor(0x15, 0x17, 0x1A);
        public static readonly TerminalColor Surface = new TerminalColor(0x1B, 0x1E, 0x22);
        public static readonly TerminalColor Elevated = new TerminalColor(0x23, 0x27, 0x2C);

        public static readonly TerminalColor StrokeSubtle = new TerminalColor(0x22, 0x25, 0x2A);
        public static readonly TerminalColor StrokeDefault = new TerminalColor(0x31, 0x36, 0x3C);

        public static readonly TerminalColor TextPrimary = new TerminalColor(0xE6, 0xE8, 0xEA);
        public static readonly TerminalColor TextSecondary = new TerminalColor(0x9A, 0xA1, 0xA9);
        public static readonly TerminalColor TextTertiary = new TerminalColor(0x6A, 0x71, 0x78);

        public static readonly TerminalColor Accent = new TerminalColor(0xE4, 0xA1, 0x30);
        public static readonly TerminalColor AccentLight = new TerminalColor(0xF0, 0xB2, 0x4A);
        public static readonly TerminalColor AccentInk = new TerminalColor(0x17, 0x18, 0x1A);

        public static readonly TerminalColor Success = new TerminalColor(0x5C, 0xB6, 0x83);
        public static readonly TerminalColor Warning = new TerminalColor(0xDD, 0xA4, 0x3C);
        public static readonly TerminalColor Error = new TerminalColor(0xE5, 0x69, 0x6A);
    }
}
