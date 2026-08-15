using System.Text;
using BetterTerminal.Terminal;

namespace BetterTerminal.Tests
{
    public sealed class TerminalWriter
    {
        public TerminalWriter(int columns, int rows, int scrollback)
        {
            Grid = new CellGrid(columns, rows, scrollback);
            Parser = new VtParser(Grid);
        }

        public CellGrid Grid { get; private set; }

        public VtParser Parser { get; private set; }

        public void Write(string text)
        {
            char[] buffer = text.ToCharArray();
            Parser.Parse(buffer, buffer.Length);
        }

        public string RowText(int line)
        {
            TerminalCell[] cells;
            long version;
            if (!Grid.TryGetLine(line, out cells, out version))
            {
                return string.Empty;
            }

            StringBuilder text = new StringBuilder();
            foreach (TerminalCell cell in cells)
            {
                text.Append(cell.Character == '\0' ? ' ' : cell.Character);
            }

            return text.ToString().TrimEnd();
        }

        public TerminalCell CellAt(int line, int column)
        {
            TerminalCell[] cells;
            long version;
            if (!Grid.TryGetLine(line, out cells, out version) || column >= cells.Length)
            {
                return new TerminalCell();
            }

            return cells[column];
        }

        public bool IsWrapped(int line)
        {
            TerminalCell[] cells;
            long version;
            return Grid.TryGetLine(line, out cells, out version) && CellGrid.IsWrapped(cells);
        }

        public int LineHolding(string text)
        {
            for (int line = 0; line < Grid.TotalLines; line++)
            {
                if (RowText(line).Contains(text))
                {
                    return line;
                }
            }

            return -1;
        }

        public TerminalLink LinkAt(int line, int column)
        {
            return Grid.Links.Find(Grid, line, column, 0, Grid.TotalLines, true);
        }
    }
}
