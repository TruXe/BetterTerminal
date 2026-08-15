using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalLinkMap
    {
        private const int MaxLogicalRows = 8;

        private readonly Dictionary<ushort, TerminalLink> _declared = new Dictionary<ushort, TerminalLink>();
        private readonly Dictionary<string, ushort> _groups = new Dictionary<string, ushort>(StringComparer.Ordinal);

        private readonly ConditionalWeakTable<TerminalCell[], LogicalScan> _scans =
            new ConditionalWeakTable<TerminalCell[], LogicalScan>();

        private ushort _nextDeclaredId = 1;
        private int _nextDetectedId = -1;

        public ushort Open(string uri, string groupKey)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return 0;
            }

            ushort id;
            if (!string.IsNullOrEmpty(groupKey) && _groups.TryGetValue(groupKey, out id))
            {
                TerminalLink grouped;
                if (_declared.TryGetValue(id, out grouped) && string.Equals(grouped.Uri, uri, StringComparison.Ordinal))
                {
                    return id;
                }
            }

            id = NextDeclaredId();
            _declared[id] = new TerminalLink(id, uri, TerminalLinkOrigin.Declared);

            if (!string.IsNullOrEmpty(groupKey))
            {
                _groups[groupKey] = id;
            }

            return id;
        }

        public string UriOf(ushort id)
        {
            TerminalLink link;
            return _declared.TryGetValue(id, out link) ? link.Uri : null;
        }

        public TerminalLink Find(CellGrid grid, int line, int column, int firstLine, int rows, bool detect)
        {
            TerminalCell[] cells;
            long version;
            if (grid == null || column < 0 || !grid.TryGetLine(line, out cells, out version))
            {
                return null;
            }

            column = LeadColumn(cells, column);

            if (column < cells.Length && cells[column].LinkId != 0)
            {
                return Declared(grid, cells[column].LinkId, firstLine, rows);
            }

            return detect ? Detected(grid, line, column) : null;
        }

        public List<TerminalLink> Visible(CellGrid grid, int firstLine, int rows, bool detect)
        {
            List<TerminalLink> links = new List<TerminalLink>();
            if (grid == null)
            {
                return links;
            }

            HashSet<int> seen = new HashSet<int>();

            for (int line = firstLine; line < firstLine + rows; line++)
            {
                TerminalCell[] cells;
                long version;
                if (!grid.TryGetLine(line, out cells, out version))
                {
                    continue;
                }

                for (int column = 0; column < cells.Length; column++)
                {
                    ushort id = cells[column].LinkId;
                    if (id == 0 || !seen.Add(id))
                    {
                        continue;
                    }

                    TerminalLink declared = Declared(grid, id, firstLine, rows);
                    if (declared != null)
                    {
                        links.Add(declared);
                    }
                }

                if (!detect)
                {
                    continue;
                }

                int start;
                LogicalScan scan = ScanOf(grid, line, out start);
                if (scan == null)
                {
                    continue;
                }

                foreach (DetectedLink detected in scan.Links)
                {
                    if (!seen.Add(detected.Id))
                    {
                        continue;
                    }

                    TerminalLink link = Materialise(detected, start);
                    if (Touches(link, firstLine, rows))
                    {
                        links.Add(link);
                    }
                }
            }

            return links;
        }

        private static bool Touches(TerminalLink link, int firstLine, int rows)
        {
            foreach (TerminalLinkRange range in link.Ranges)
            {
                if (range.Line >= firstLine && range.Line < firstLine + rows)
                {
                    return true;
                }
            }

            return false;
        }

        private static int LeadColumn(TerminalCell[] cells, int column)
        {
            if (column > 0 && column < cells.Length && (cells[column].Flags & CellFlags.WideTrailing) != 0)
            {
                return column - 1;
            }

            return column;
        }

        private ushort NextDeclaredId()
        {
            if (_nextDeclaredId == 0)
            {
                _nextDeclaredId = 1;
            }

            ushort id = _nextDeclaredId++;
            _declared.Remove(id);
            return id;
        }

        private int NextDetectedId()
        {
            if (_nextDetectedId >= 0)
            {
                _nextDetectedId = -1;
            }

            return _nextDetectedId--;
        }

        private TerminalLink Declared(CellGrid grid, ushort id, int firstLine, int rows)
        {
            TerminalLink source;
            if (!_declared.TryGetValue(id, out source))
            {
                return null;
            }

            TerminalLink link = new TerminalLink(source.Id, source.Uri, TerminalLinkOrigin.Declared);
            StringBuilder text = new StringBuilder();

            for (int line = firstLine; line < firstLine + rows; line++)
            {
                TerminalCell[] cells;
                long version;
                if (!grid.TryGetLine(line, out cells, out version))
                {
                    continue;
                }

                int column = 0;
                while (column < cells.Length)
                {
                    if (cells[column].LinkId != id)
                    {
                        column++;
                        continue;
                    }

                    int start = column;
                    while (column < cells.Length && cells[column].LinkId == id)
                    {
                        if ((cells[column].Flags & CellFlags.WideTrailing) == 0)
                        {
                            text.Append(cells[column].Character == '\0' ? ' ' : cells[column].Character);
                        }

                        column++;
                    }

                    link.Ranges.Add(new TerminalLinkRange(line, start, column));
                }
            }

            link.Text = text.ToString().Trim();
            return link;
        }

        private TerminalLink Detected(CellGrid grid, int line, int column)
        {
            int start;
            LogicalScan scan = ScanOf(grid, line, out start);
            if (scan == null)
            {
                return null;
            }

            int offset = line - start;

            foreach (DetectedLink detected in scan.Links)
            {
                foreach (TerminalLinkRange range in detected.Segments)
                {
                    if (range.Covers(offset, column))
                    {
                        return Materialise(detected, start);
                    }
                }
            }

            return null;
        }

        private static TerminalLink Materialise(DetectedLink detected, int startLine)
        {
            TerminalLink link = new TerminalLink(detected.Id, detected.Uri, TerminalLinkOrigin.Detected);
            link.Text = detected.Uri;

            foreach (TerminalLinkRange range in detected.Segments)
            {
                link.Ranges.Add(new TerminalLinkRange(startLine + range.Line, range.Start, range.End));
            }

            return link;
        }

        private LogicalScan ScanOf(CellGrid grid, int line, out int start)
        {
            start = LogicalStart(grid, line);

            TerminalCell[] first;
            long version;
            if (!grid.TryGetLine(start, out first, out version))
            {
                return null;
            }

            LogicalScan scan;
            if (_scans.TryGetValue(first, out scan) && scan.IsCurrent(grid, start))
            {
                return scan;
            }

            scan = Build(grid, start);
            _scans.Remove(first);
            _scans.Add(first, scan);
            return scan;
        }

        private static int LogicalStart(CellGrid grid, int line)
        {
            int start = line;
            int limit = Math.Max(0, line - (MaxLogicalRows - 1));

            while (start > limit)
            {
                TerminalCell[] previous;
                long version;
                if (!grid.TryGetLine(start - 1, out previous, out version) || !CellGrid.IsWrapped(previous))
                {
                    break;
                }

                start--;
            }

            return start;
        }

        private LogicalScan Build(CellGrid grid, int start)
        {
            List<TerminalCell[]> rows = new List<TerminalCell[]>();
            List<long> versions = new List<long>();

            int line = start;
            while (rows.Count < MaxLogicalRows)
            {
                TerminalCell[] cells;
                long version;
                if (!grid.TryGetLine(line, out cells, out version))
                {
                    break;
                }

                rows.Add(cells);
                versions.Add(version);

                if (!CellGrid.IsWrapped(cells))
                {
                    break;
                }

                line++;
            }

            int capacity = 0;
            foreach (TerminalCell[] cells in rows)
            {
                capacity += cells.Length;
            }

            char[] text = new char[capacity];
            int[] rowOf = new int[capacity];
            int[] columnOf = new int[capacity];
            bool[] claimed = new bool[capacity];
            int count = 0;

            for (int index = 0; index < rows.Count; index++)
            {
                TerminalCell[] cells = rows[index];
                for (int column = 0; column < cells.Length; column++)
                {
                    if ((cells[column].Flags & CellFlags.WideTrailing) != 0)
                    {
                        continue;
                    }

                    char character = cells[column].Character;
                    text[count] = character == '\0' ? ' ' : character;
                    rowOf[count] = index;
                    columnOf[count] = column;
                    claimed[count] = cells[column].LinkId != 0;
                    count++;
                }
            }

            List<TerminalLinkSpan> spans = new List<TerminalLinkSpan>();
            TerminalLinkDetector.Scan(text, count, claimed, spans);

            LogicalScan scan = new LogicalScan();
            scan.Rows = rows.ToArray();
            scan.Versions = versions.ToArray();
            scan.Links = new List<DetectedLink>();

            foreach (TerminalLinkSpan span in spans)
            {
                scan.Links.Add(new DetectedLink(
                    NextDetectedId(),
                    span.Uri,
                    Segments(span, rowOf, columnOf, scan.Rows)));
            }

            return scan;
        }

        private static List<TerminalLinkRange> Segments(TerminalLinkSpan span, int[] rowOf, int[] columnOf,
            TerminalCell[][] rows)
        {
            List<TerminalLinkRange> segments = new List<TerminalLinkRange>();
            int index = span.Start;

            while (index < span.End)
            {
                int row = rowOf[index];
                int first = columnOf[index];
                int last = first;

                while (index < span.End && rowOf[index] == row)
                {
                    last = columnOf[index];
                    index++;
                }

                int end = last + 1;
                TerminalCell[] cells = rows[row];
                if (end < cells.Length && (cells[end].Flags & CellFlags.WideTrailing) != 0)
                {
                    end++;
                }

                segments.Add(new TerminalLinkRange(row, first, end));
            }

            return segments;
        }

        private sealed class DetectedLink
        {
            public DetectedLink(int id, string uri, List<TerminalLinkRange> segments)
            {
                Id = id;
                Uri = uri;
                Segments = segments;
            }

            public int Id { get; private set; }

            public string Uri { get; private set; }

            public List<TerminalLinkRange> Segments { get; private set; }
        }

        private sealed class LogicalScan
        {
            public TerminalCell[][] Rows;
            public long[] Versions;
            public List<DetectedLink> Links;

            public bool IsCurrent(CellGrid grid, int start)
            {
                for (int index = 0; index < Rows.Length; index++)
                {
                    TerminalCell[] cells;
                    long version;
                    if (!grid.TryGetLine(start + index, out cells, out version))
                    {
                        return false;
                    }

                    if (!ReferenceEquals(cells, Rows[index]) || version != Versions[index])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
