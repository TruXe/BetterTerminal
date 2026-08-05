using System;

namespace BetterTerminal.Terminal
{
    // All mutating members require the caller to hold SyncRoot: the VT parser runs on the reader
    // thread while the renderer reads the same lines on the UI thread.
    public sealed class CellGrid
    {
        private readonly object _sync = new object();
        private readonly int _scrollbackCapacity;
        private readonly TerminalCell[][] _scrollback;
        private readonly long[] _scrollbackVersions;

        private TerminalCell[][] _lines;
        private long[] _lineVersions;
        private TerminalCell[][] _mainLines;
        private long[] _mainLineVersions;

        private int _scrollbackStart;
        private int _scrollbackCount;
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
            _scrollback = new TerminalCell[_scrollbackCapacity][];
            _scrollbackVersions = new long[_scrollbackCapacity];

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

        public bool AlternateScreenActive { get; private set; }

        public int CurrentForeground { get; set; }

        public int CurrentBackground { get; set; }

        public CellFlags CurrentFlags { get; set; }

        public int ScrollbackCount
        {
            get { return _scrollbackCount; }
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
                int slot = (_scrollbackStart + absoluteIndex) % _scrollbackCapacity;
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
                _pendingWrap = false;
                CursorColumn = 0;
                LineFeed();
            }

            TerminalCell[] line = _lines[CursorRow];
            line[CursorColumn].Character = character;
            line[CursorColumn].Foreground = CurrentForeground;
            line[CursorColumn].Background = CurrentBackground;
            line[CursorColumn].Flags = CurrentFlags;
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
            int slot = (_scrollbackStart + _scrollbackCount) % _scrollbackCapacity;
            if (_scrollbackCount == _scrollbackCapacity)
            {
                slot = _scrollbackStart;
                _scrollbackStart = (_scrollbackStart + 1) % _scrollbackCapacity;
            }
            else
            {
                _scrollbackCount++;
            }

            _scrollback[slot] = line;
            _scrollbackVersions[slot] = version;
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
