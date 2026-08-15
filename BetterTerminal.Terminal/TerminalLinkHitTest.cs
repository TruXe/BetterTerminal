using System;

namespace BetterTerminal.Terminal
{
    public static class TerminalLinkHitTest
    {
        public static int RowAt(double y, double cellHeight, int rows)
        {
            if (cellHeight <= 0 || rows <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(rows - 1, (int)(y / cellHeight)));
        }

        public static int ColumnAt(double x, double cellWidth, int columns)
        {
            if (cellWidth <= 0 || columns <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(columns - 1, (int)(x / cellWidth)));
        }

        public static int LineAt(int row, int totalLines, int rows, int scrollOffset)
        {
            return Math.Max(0, totalLines - rows - scrollOffset) + row;
        }
    }
}
