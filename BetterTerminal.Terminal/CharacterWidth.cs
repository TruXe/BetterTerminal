namespace BetterTerminal.Terminal
{
    public static class CharacterWidth
    {
        public static bool IsWide(char character)
        {
            if (character < 0x1100)
            {
                return false;
            }

            return (character >= 0x1100 && character <= 0x115F)
                || (character >= 0x2E80 && character <= 0x303E)
                || (character >= 0x3041 && character <= 0x33FF)
                || (character >= 0x3400 && character <= 0x4DBF)
                || (character >= 0x4E00 && character <= 0x9FFF)
                || (character >= 0xA000 && character <= 0xA4CF)
                || (character >= 0xAC00 && character <= 0xD7A3)
                || (character >= 0xF900 && character <= 0xFAFF)
                || (character >= 0xFE10 && character <= 0xFE19)
                || (character >= 0xFE30 && character <= 0xFE6F)
                || (character >= 0xFF00 && character <= 0xFF60)
                || (character >= 0xFFE0 && character <= 0xFFE6);
        }
    }
}
