using BetterTerminal.Terminal;

namespace BetterTerminal.Tests
{
    public static class HitTestTests
    {
        private const double CellWidth = 8;
        private const double CellHeight = 16;

        private static readonly string Escape = ((char)0x1b).ToString();
        private static readonly string Bell = ((char)0x07).ToString();

        public static void Run(TestRun run)
        {
            run.Section("Finding the link under the pointer");

            Mapping(run);
            ScrolledBack(run);
            DoubleWidth(run);
            Resized(run);
        }

        private static void Mapping(TestRun run)
        {
            run.Equal("the pointer maps to the row it is over", 2,
                TerminalLinkHitTest.RowAt(2.5 * CellHeight, CellHeight, 5));
            run.Equal("a pointer above the first row stays on it", 0,
                TerminalLinkHitTest.RowAt(-30, CellHeight, 5));
            run.Equal("a pointer below the last row stays on it", 4,
                TerminalLinkHitTest.RowAt(900, CellHeight, 5));
            run.Equal("the pointer maps to the column it is over", 7,
                TerminalLinkHitTest.ColumnAt(7.9 * CellWidth, CellWidth, 40));
            run.Equal("a pointer past the last column stays on it", 39,
                TerminalLinkHitTest.ColumnAt(900, CellWidth, 40));
        }

        private static void ScrolledBack(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 200);
            for (int index = 0; index < 12; index++)
            {
                writer.Write("row " + index + (index == 2 ? " http://example.com/x" : string.Empty));
                writer.Write(((char)0x0d).ToString() + (char)0x0a);
            }

            int line = writer.LineHolding("http://example.com/x");
            int rows = writer.Grid.Rows;
            int total = writer.Grid.TotalLines;
            int offset = total - rows - line;

            run.Check("the address is in the history, not on the screen", offset > 0);

            int row = TerminalLinkHitTest.RowAt(4, CellHeight, rows);
            int mapped = TerminalLinkHitTest.LineAt(row, total, rows, offset);
            run.Equal("at a scrolled position the top row is the line scrolled to", line, mapped);

            int column = TerminalLinkHitTest.ColumnAt((8 * CellWidth) + 2, CellWidth, writer.Grid.Columns);
            TerminalLink link = writer.Grid.Links.Find(writer.Grid, mapped, column, mapped, rows, true);

            run.Equal("a link in the history is found under the pointer", "http://example.com/x",
                link == null ? null : link.Uri);
            run.Check("the link points at the cells it is printed in",
                DetectionTests.CellsMatch(writer, link));
        }

        private static void DoubleWidth(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(20, 3, 50);
            string wide = ((char)0x65e5).ToString() + (char)0x672c;
            writer.Write(Escape + "]8;;https://example.com/wide" + Bell + wide + Escape + "]8;;" + Bell);

            run.Check("a double width character takes the cell beside it",
                (writer.CellAt(0, 1).Flags & CellFlags.WideTrailing) != 0);
            run.Check("the next character starts two cells along",
                writer.CellAt(0, 2).Character == (char)0x672c);

            TerminalLink lead = writer.Grid.Links.Find(writer.Grid, 0, 0, 0, 3, true);
            TerminalLink trailing = writer.Grid.Links.Find(writer.Grid, 0, 1, 0, 3, true);

            run.Equal("the left half of a double width cell finds the link",
                "https://example.com/wide", lead == null ? null : lead.Uri);
            run.Check("the right half of a double width cell finds the same link",
                lead != null && trailing != null && lead.Id == trailing.Id);
        }

        private static void Resized(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 100);
            writer.Write("go https://example.com/abc");

            TerminalLink before = writer.LinkAt(0, 6);
            run.Equal("the address is a link to start with", "https://example.com/abc",
                before == null ? null : before.Uri);

            writer.Grid.Resize(60, 5);
            TerminalLink wider = writer.LinkAt(0, 6);
            run.Equal("a wider window keeps the link", "https://example.com/abc",
                wider == null ? null : wider.Uri);
            run.Check("a wider window keeps the link over its own cells",
                DetectionTests.CellsMatch(writer, wider));

            writer.Grid.Resize(14, 5);
            TerminalLink narrower = writer.LinkAt(0, 6);
            run.Check("a narrower window leaves the link over its own cells",
                DetectionTests.CellsMatch(writer, narrower));
            run.Check("a narrower window does not claim the row runs on",
                !writer.IsWrapped(0));
        }
    }
}
