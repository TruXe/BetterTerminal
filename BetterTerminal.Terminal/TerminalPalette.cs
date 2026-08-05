namespace BetterTerminal.Terminal
{
    public static class TerminalPalette
    {
        private static readonly int[] Colors = BuildXterm256();

        public static int Get(int index)
        {
            if (index < 0 || index >= Colors.Length)
            {
                return 0;
            }

            return Colors[index];
        }

        public static int FromRgb(int red, int green, int blue)
        {
            return unchecked((int)0xFF000000) | (red << 16) | (green << 8) | blue;
        }

        private static int[] BuildXterm256()
        {
            int[] colors = new int[256];

            // Campbell, the Windows console scheme; kept as the single source of ANSI colour truth.
            int[] baseColors =
            {
                0x0C0C0C, 0xC50F1F, 0x13A10E, 0xC19C00, 0x0037DA, 0x881798, 0x3A96DD, 0xCCCCCC,
                0x767676, 0xE74856, 0x16C60C, 0xF9F1A5, 0x3B78FF, 0xB4009E, 0x61D6D6, 0xF2F2F2
            };

            for (int i = 0; i < baseColors.Length; i++)
            {
                colors[i] = unchecked((int)0xFF000000) | baseColors[i];
            }

            int[] levels = { 0, 95, 135, 175, 215, 255 };
            int next = 16;
            for (int r = 0; r < 6; r++)
            {
                for (int g = 0; g < 6; g++)
                {
                    for (int b = 0; b < 6; b++)
                    {
                        colors[next++] = FromRgb(levels[r], levels[g], levels[b]);
                    }
                }
            }

            for (int i = 0; i < 24; i++)
            {
                int level = 8 + (i * 10);
                colors[next++] = FromRgb(level, level, level);
            }

            return colors;
        }
    }
}
