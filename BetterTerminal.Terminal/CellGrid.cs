using System;

namespace BetterTerminal.Terminal
{
    // All mutating members require the caller to hold SyncRoot: the VT parser runs on the reader
    // thread while the renderer reads the same lines on the UI thread.
    public sealed class CellGrid
    {
        // The history is allowed to be very large, so it is not laid out in advance: a pane that
        // never scrolls must not pay for a million lines it will not write. The ring starts small and
        // doubles until it reaches the capacity it was asked for.
        private const int InitialScrollbackSlots = 4096;

        private readonly object _sync = new object();
        private readonly int _scrollbackCapacity;
        private readonly TerminalLinkMap _links = new TerminalLinkMap();

        private TerminalCell[][] _scrollback;
        private long[] _scrollbackVersions;

        private TerminalCell[][] _lines;
        private long[] _lineVersions;
        private TerminalCell[][] _mainLines;
        private long[] _mainLineVersions;

        private int _scrollbackStart;
        private int _scrollbackCount;
        private long _scrolledLines;

        // Cache behind FirstUsedLine: the answer once known, and how much of the history has already
        // been proved blank so it is never re-examined.
        private int _firstUsed = -1;
        private int _blankPrefix;
        private long _versionCounter;

        private int _columns;
        private int _rows;
        private int _scrollTop;
        private int _scrollBottom;
        private int _savedColumn;
        private int _savedRow;
        private int _mainCursorColumn;
        private int _mainCursorRow;
        private bool _pendingWrap;

        public CellGrid(int columns, int rows, int scrollbackCapacity)
        {
            _columns = Math.Max(1, columns);
            _rows = Math.Max(1, rows);
            _scrollbackCapacity = Math.Max(0, scrollbackCapacity);

            int slots = Math.Min(_scrollbackCapacity, InitialScrollbackSlots);
            _scrollback = new TerminalCell[slots][];
            _scrollbackVersions = new long[slots];

            _lines = new TerminalCell[_rows][];
            _lineVersions = new long[_rows];
            for (int row = 0; row < _rows; row++)
            {
                _lines[row] = CreateBlankLine(_columns);
                _lineVersions[row] = ++_versionCounter;
            }

            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            AutoWrap = true;
            CursorVisible = true;
            ResetAttributes();
        }

        public object SyncRoot
        {
            get { return _sync; }
        }

        public int Columns
        {
            get { return _columns; }
        }

        public int Rows
        {
            get { return _rows; }
        }

        public int CursorColumn { get; private set; }

        public int CursorRow { get; private set; }

        public bool CursorVisible { get; set; }

        public bool AutoWrap { get; set; }

        public bool ApplicationCursorKeys { get; set; }

        public bool BracketedPaste { get; set; }

        // Set while the console host has asked for whole key events instead of bare characters.
        public bool Win32InputMode { get; set; }

        public bool AlternateScreenActive { get; private set; }

        public int CurrentForeground { get; set; }

        public int CurrentBackground { get; set; }

        public CellFlags CurrentFlags { get; set; }

        public ushort CurrentLinkId { get; set; }

        public TerminalLinkMap Links
        {
            get { return _links; }
        }

        /// <summary>
        /// The oldest line worth scrolling to: the first one in the history that has anything on it.
        /// Blank lines reach the history honestly - shrinking the pane pushes the top of the screen
        /// into it whether or not anything was written there - and without this the view scrolls up
        /// into that emptiness, past the banner the session opened with, which reads as a bug.
        ///
        /// Scanned once and remembered. <see cref="_blankPrefix"/> is how far the scan has already
        /// proved blank, so new history is only ever examined once and the cost stays flat.
        /// </summary>
        public int FirstUsedLine
        {
            get
            {
                if (_firstUsed >= 0)
                {
                    return _firstUsed;
                }

                while (_blankPrefix < _scrollbackCount)
                {
                    int slot = (_scrollbackStart + _blankPrefix) % _scrollback.Length;
                    if (!IsBlank(_scrollback[slot]))
                    {
                        _firstUsed = _blankPrefix;
                        return _firstUsed;
                    }

                    _blankPrefix++;
                }

                // Nothing written yet: the live screen is the ceiling.
                return _scrollbackCount;
            }
        }

        public int ScrollbackCount
        {
            get { return _scrollbackCount; }
        }

        /// <summary>
        /// How many lines have left the live screen for the history since the session opened. It only
        /// ever grows, which is what a reader scrolled up needs: once the history is full, pushing a
        /// line drops the oldest one and every absolute index moves down by one, so the count of lines
        /// pushed is the only honest measure of how far the content under the reader has travelled.
        /// </summary>
        public long ScrolledLines
        {
            get { return _scrolledLines; }
        }

        public int TotalLines
        {
            get { return _scrollbackCount + _rows; }
        }

        public bool TryGetLine(int absoluteIndex, out TerminalCell[] cells, out long version)
        {
            if (absoluteIndex < 0 || absoluteIndex >= TotalLines)
            {
                cells = null;
                version = 0;
                return false;
            }

            if (absoluteIndex < _scrollbackCount)
            {
                int slot = (_scrollbackStart + absoluteIndex) % _scrollback.Length;
                cells = _scrollback[slot];
                version = _scrollbackVersions[slot];
                return true;
            }

            int row = absoluteIndex - _scrollbackCount;
            cells = _lines[row];
            version = _lineVersions[row];
            return true;
        }

        public void ResetAttributes()
        {
            CurrentForeground = 0;
            CurrentBackground = 0;
            CurrentFlags = CellFlags.None;
        }

        public void Write(char character)
        {
            if (_pendingWrap)
            {
                WrapLine();
            }

            int width = CharacterWidth.IsWide(character) ? 2 : 1;
            if (width == 2 && CursorColumn + 1 >= _columns && AutoWrap && _columns > 1)
            {
                WrapLine();
            }

            TerminalCell[] line = _lines[CursorRow];
            line[CursorColumn].Character = character;
            line[CursorColumn].Foreground = CurrentForeground;
            line[CursorColumn].Background = CurrentBackground;
            line[CursorColumn].Flags = CurrentFlags;
            line[CursorColumn].LinkId = CurrentLinkId;

            if (width == 2 && CursorColumn + 1 < _columns)
            {
                line[CursorColumn + 1].Character = '\0';
                line[CursorColumn + 1].Foreground = CurrentForeground;
                line[CursorColumn + 1].Background = CurrentBackground;
                line[CursorColumn + 1].Flags = CurrentFlags | CellFlags.WideTrailing;
                line[CursorColumn + 1].LinkId = CurrentLinkId;
                CursorColumn++;
            }

            _lineVersions[CursorRow] = ++_versionCounter;

            if (CursorColumn + 1 >= _columns)
            {
                if (AutoWrap)
                {
                    _pendingWrap = true;
                }
            }
            else
            {
                CursorColumn++;
            }
        }

        public static bool IsWrapped(TerminalCell[] line)
        {
            return line != null && line.Length > 0
                && (line[line.Length - 1].Flags & CellFlags.LineWrapped) != 0;
        }

        private void WrapLine()
        {
            _pendingWrap = false;

            TerminalCell[] line = _lines[CursorRow];
            if (line.Length > 0)
            {
                line[line.Length - 1].Flags |= CellFlags.LineWrapped;
                _lineVersions[CursorRow] = ++_versionCounter;
            }

            CursorColumn = 0;
            LineFeed();
        }

        public void CarriageReturn()
        {
            CursorColumn = 0;
            _pendingWrap = false;
        }

        public void LineFeed()
        {
            _pendingWrap = false;

            if (CursorRow == _scrollBottom)
            {
                ScrollUp(1);
            }
            else if (CursorRow < _rows - 1)
            {
                CursorRow++;
            }
        }

        public void ReverseLineFeed()
        {
            _pendingWrap = false;

            if (CursorRow == _scrollTop)
            {
                ScrollDown(1);
            }
            else if (CursorRow > 0)
            {
                CursorRow--;
            }
        }

        public void Backspace()
        {
            _pendingWrap = false;
            if (CursorColumn > 0)
            {
                CursorColumn--;
            }
        }

        public void Tab()
        {
            _pendingWrap = false;
            int next = ((CursorColumn / 8) + 1) * 8;
            CursorColumn = Math.Min(next, _columns - 1);
        }

        public void SetCursor(int column, int row)
        {
            _pendingWrap = false;
            CursorColumn = Clamp(column, 0, _columns - 1);
            CursorRow = Clamp(row, 0, _rows - 1);
        }

        public void MoveCursor(int columnDelta, int rowDelta)
        {
            SetCursor(CursorColumn + columnDelta, CursorRow + rowDelta);
        }

        public void SaveCursor()
        {
            _savedColumn = CursorColumn;
            _savedRow = CursorRow;
        }

        public void RestoreCursor()
        {
            SetCursor(_savedColumn, _savedRow);
        }

        public void SetScrollRegion(int top, int bottom)
        {
            _scrollTop = Clamp(top, 0, _rows - 1);
            _scrollBottom = Clamp(bottom, _scrollTop, _rows - 1);
            SetCursor(0, _scrollTop);
        }

        public void ScrollUp(int count)
        {
            for (int i = 0; i < count; i++)
            {
                TerminalCell[] recycled = _lines[_scrollTop];
                long version = _lineVersions[_scrollTop];

                if (!AlternateScreenActive && _scrollTop == 0 && _scrollbackCapacity > 0)
                {
                    PushScrollback(recycled, version);
                    recycled = CreateBlankLine(_columns);
                }
                else
                {
                    ClearLine(recycled, 0, _columns - 1);
                }

                for (int row = _scrollTop; row < _scrollBottom; row++)
                {
                    _lines[row] = _lines[row + 1];
                    _lineVersions[row] = _lineVersions[row + 1];
                }

                _lines[_scrollBottom] = recycled;
                _lineVersions[_scrollBottom] = ++_versionCounter;
            }
        }

        public void ScrollDown(int count)
        {
            for (int i = 0; i < count; i++)
            {
                TerminalCell[] recycled = _lines[_scrollBottom];
                ClearLine(recycled, 0, _columns - 1);

                for (int row = _scrollBottom; row > _scrollTop; row--)
                {
                    _lines[row] = _lines[row - 1];
                    _lineVersions[row] = _lineVersions[row - 1];
                }

                _lines[_scrollTop] = recycled;
                _lineVersions[_scrollTop] = ++_versionCounter;
            }
        }

        public void EraseInDisplay(int mode)
        {
            if (mode == 0)
            {
                EraseInLine(0);
                for (int row = CursorRow + 1; row < _rows; row++)
                {
                    ClearLine(_lines[row], 0, _columns - 1);
                    _lineVersions[row] = ++_versionCounter;
                }
            }
            else if (mode == 1)
            {
                EraseInLine(1);
                for (int row = 0; row < CursorRow; row++)
                {
                    ClearLine(_lines[row], 0, _columns - 1);
                    _lineVersions[row] = ++_versionCounter;
                }
            }
            else
            {
                for (int row = 0; row < _rows; row++)
                {
                    ClearLine(_lines[row], 0, _columns - 1);
                    _lineVersions[row] = ++_versionCounter;
                }
            }
        }

        public void EraseInLine(int mode)
        {
            TerminalCell[] line = _lines[CursorRow];
            if (mode == 0)
            {
                ClearLine(line, CursorColumn, _columns - 1);
            }
            else if (mode == 1)
            {
                ClearLine(line, 0, CursorColumn);
            }
            else
            {
                ClearLine(line, 0, _columns - 1);
            }

            _lineVersions[CursorRow] = ++_versionCounter;
        }

        public void EraseCharacters(int count)
        {
            ClearLine(_lines[CursorRow], CursorColumn, Math.Min(_columns - 1, CursorColumn + count - 1));
            _lineVersions[CursorRow] = ++_versionCounter;
        }

        public void InsertCharacters(int count)
        {
            TerminalCell[] line = _lines[CursorRow];
            for (int column = _columns - 1; column >= CursorColumn + count; column--)
            {
                line[column] = line[column - count];
            }

            ClearLine(line, CursorColumn, Math.Min(_columns - 1, CursorColumn + count - 1));
            _lineVersions[CursorRow] = ++_versionCounter;
        }

        public void DeleteCharacters(int count)
        {
            TerminalCell[] line = _lines[CursorRow];
            for (int column = CursorColumn; column < _columns; column++)
            {
                int source = column + count;
                line[column] = source < _columns ? line[source] : CreateBlankCell();
            }

            _lineVersions[CursorRow] = ++_versionCounter;
        }

        public void InsertLines(int count)
        {
            if (CursorRow < _scrollTop || CursorRow > _scrollBottom)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                TerminalCell[] recycled = _lines[_scrollBottom];
                ClearLine(recycled, 0, _columns - 1);

                for (int row = _scrollBottom; row > CursorRow; row--)
                {
                    _lines[row] = _lines[row - 1];
                    _lineVersions[row] = _lineVersions[row - 1];
                }

                _lines[CursorRow] = recycled;
                _lineVersions[CursorRow] = ++_versionCounter;
            }
        }

        public void DeleteLines(int count)
        {
            if (CursorRow < _scrollTop || CursorRow > _scrollBottom)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                TerminalCell[] recycled = _lines[CursorRow];
                ClearLine(recycled, 0, _columns - 1);

                for (int row = CursorRow; row < _scrollBottom; row++)
                {
                    _lines[row] = _lines[row + 1];
                    _lineVersions[row] = _lineVersions[row + 1];
                }

                _lines[_scrollBottom] = recycled;
                _lineVersions[_scrollBottom] = ++_versionCounter;
            }
        }

        public void EnterAlternateScreen()
        {
            if (AlternateScreenActive)
            {
                return;
            }

            _mainLines = _lines;
            _mainLineVersions = _lineVersions;
            _mainCursorColumn = CursorColumn;
            _mainCursorRow = CursorRow;

            _lines = new TerminalCell[_rows][];
            _lineVersions = new long[_rows];
            for (int row = 0; row < _rows; row++)
            {
                _lines[row] = CreateBlankLine(_columns);
                _lineVersions[row] = ++_versionCounter;
            }

            AlternateScreenActive = true;
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            SetCursor(0, 0);
        }

        public void LeaveAlternateScreen()
        {
            if (!AlternateScreenActive || _mainLines == null)
            {
                return;
            }

            _lines = _mainLines;
            _lineVersions = _mainLineVersions;
            _mainLines = null;
            _mainLineVersions = null;
            AlternateScreenActive = false;

            for (int row = 0; row < _rows; row++)
            {
                _lineVersions[row] = ++_versionCounter;
            }

            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            SetCursor(_mainCursorColumn, _mainCursorRow);
        }

        public void Resize(int columns, int rows)
        {
            columns = Math.Max(1, columns);
            rows = Math.Max(1, rows);
            if (columns == _columns && rows == _rows)
            {
                return;
            }

            TerminalCell[][] resized = new TerminalCell[rows][];
            long[] versions = new long[rows];

            int copyRows = Math.Min(rows, _rows);
            int firstSource = _rows - copyRows;

            for (int row = 0; row < copyRows; row++)
            {
                resized[row] = ResizeLine(_lines[firstSource + row], columns);
                versions[row] = ++_versionCounter;
            }

            for (int row = copyRows; row < rows; row++)
            {
                resized[row] = CreateBlankLine(columns);
                versions[row] = ++_versionCounter;
            }

            if (!AlternateScreenActive && _scrollbackCapacity > 0)
            {
                for (int row = 0; row < firstSource; row++)
                {
                    PushScrollback(ResizeLine(_lines[row], columns), ++_versionCounter);
                }
            }

            _lines = resized;
            _lineVersions = versions;
            _columns = columns;
            _rows = rows;
            _scrollTop = 0;
            _scrollBottom = rows - 1;
            _pendingWrap = false;
            CursorColumn = Clamp(CursorColumn, 0, columns - 1);
            CursorRow = Clamp(CursorRow, 0, rows - 1);
        }

        private void PushScrollback(TerminalCell[] line, long version)
        {
            _scrolledLines++;
            line = TrimTrailingBlanks(line);

            if (_scrollbackCount == _scrollback.Length && _scrollback.Length < _scrollbackCapacity)
            {
                GrowScrollback();
            }

            int slot = (_scrollbackStart + _scrollbackCount) % _scrollback.Length;
            if (_scrollbackCount == _scrollback.Length)
            {
                slot = _scrollbackStart;
                _scrollbackStart = (_scrollbackStart + 1) % _scrollback.Length;

                // The oldest line is gone, so every absolute index below it moves down one. When the
                // line being dropped is the answer itself, the answer is unknown again - not zero,
                // which would claim the new oldest line has content without having looked.
                _firstUsed = _firstUsed > 0 ? _firstUsed - 1 : -1;
                _blankPrefix = Math.Max(0, _blankPrefix - 1);
            }
            else
            {
                _scrollbackCount++;
            }

            _scrollback[slot] = line;
            _scrollbackVersions[slot] = version;
        }

        /// <summary>
        /// Doubles the ring, in logical order, so the oldest line ends up at slot zero. Called only
        /// when the ring is full and the capacity has not been reached, which is at most once per
        /// doubling - the copying is amortised to a constant per line.
        /// </summary>
        private void GrowScrollback()
        {
            int grown = Math.Min(_scrollbackCapacity, Math.Max(1, _scrollback.Length) * 2);

            TerminalCell[][] lines = new TerminalCell[grown][];
            long[] versions = new long[grown];

            for (int index = 0; index < _scrollbackCount; index++)
            {
                int slot = (_scrollbackStart + index) % _scrollback.Length;
                lines[index] = _scrollback[slot];
                versions[index] = _scrollbackVersions[slot];
            }

            _scrollback = lines;
            _scrollbackVersions = versions;
            _scrollbackStart = 0;
        }

        /// <summary>
        /// Drops the padding off the end of a line on its way into the history. A screen line is as
        /// wide as the screen, and most of a line of output is the blank right-hand end of it; at a
        /// million lines that padding is the difference between a manageable history and a gigabyte
        /// of spaces. Cells carrying a background colour are kept, blank or not, because that colour
        /// is on screen and has to still be there when it is scrolled back to.
        /// </summary>
        private static TerminalCell[] TrimTrailingBlanks(TerminalCell[] line)
        {
            int used = line.Length;
            while (used > 0)
            {
                TerminalCell cell = line[used - 1];
                if (cell.Background != 0 || cell.Flags != CellFlags.None
                    || (cell.Character != ' ' && cell.Character != '\0'))
                {
                    break;
                }

                used--;
            }

            if (used == line.Length)
            {
                return line;
            }

            TerminalCell[] trimmed = new TerminalCell[used];
            Array.Copy(line, trimmed, used);
            return trimmed;
        }

        /// <summary>
        /// Whether a line has anything on it. Only the character matters: a run of spaces carrying
        /// a background colour is still nothing to scroll back to.
        /// </summary>
        private static bool IsBlank(TerminalCell[] line)
        {
            if (line == null)
            {
                return true;
            }

            for (int column = 0; column < line.Length; column++)
            {
                char character = line[column].Character;
                if (character != ' ' && character != '\0')
                {
                    return false;
                }
            }

            return true;
        }

        private static TerminalCell[] ResizeLine(TerminalCell[] line, int columns)
        {
            if (line.Length == columns)
            {
                return line;
            }

            TerminalCell[] resized = CreateBlankLine(columns);
            int copy = Math.Min(columns, line.Length);
            Array.Copy(line, resized, copy);

            if (copy > 0)
            {
                resized[copy - 1].Flags &= ~CellFlags.LineWrapped;
            }

            return resized;
        }

        private static TerminalCell[] CreateBlankLine(int columns)
        {
            TerminalCell[] line = new TerminalCell[columns];
            for (int column = 0; column < columns; column++)
            {
                line[column] = CreateBlankCell();
            }

            return line;
        }

        private static TerminalCell CreateBlankCell()
        {
            TerminalCell cell = new TerminalCell();
            cell.Character = ' ';
            return cell;
        }

        private void ClearLine(TerminalCell[] line, int from, int to)
        {
            TerminalCell blank = CreateBlankCell();
            blank.Background = CurrentBackground;

            for (int column = Math.Max(0, from); column <= Math.Min(to, line.Length - 1); column++)
            {
                line[column] = blank;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
