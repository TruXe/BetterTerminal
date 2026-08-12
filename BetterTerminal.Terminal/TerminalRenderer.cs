using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalRenderer : FrameworkElement
    {
        private const double MinimumFontSize = 8;
        private const double MaximumFontSize = 36;
        private const int WheelLinesPerNotch = 3;

        // What Ctrl+V is on the wire; a program reading whole key events is told the key instead.
        private const char PasteControlCharacter = '\x16';

        // A thin lane on the right edge. The lane is wider than the thumb so it is easy to grab; the
        // thumb itself stays slim to keep it unobtrusive over the output.
        private const double ScrollbarLaneWidth = 12;
        private const double ScrollbarThumbWidth = 4;
        private const double ScrollbarMinThumb = 28;

        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);
        private static readonly TimeSpan CaretBlinkInterval = TimeSpan.FromMilliseconds(530);

        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _scrollbar = new DrawingVisual();
        private readonly DispatcherTimer _frameTimer;
        private readonly DispatcherTimer _caretTimer;
        private readonly Dictionary<int, Brush> _brushes = new Dictionary<int, Brush>();

        private ITerminalSession _session;
        private CellGrid _grid;
        private GlyphTypeface _regularFace;
        private GlyphTypeface _boldFace;
        private long[] _renderedVersions;

        private string _fontFamily = "Cascadia Mono, Consolas";
        private double _fontSize = 14;
        private double _cellWidth = 8;
        private double _cellHeight = 16;
        private double _baseline = 12;
        private float _pixelsPerDip = 1;
        private int _outputPending;
        private int _scrollOffset;

        // How much of the history had already been written when the offset above was last honoured.
        // The difference against the grid is how far the content under a reader who has scrolled up
        // has been pushed since, and the offset is corrected by exactly that much.
        private long _anchoredScrolledLines;
        private int _columns;
        private int _rows;
        private bool _fullRedraw = true;
        private bool _caretOn = true;
        private bool _caretBlinks = true;
        private bool _selecting;
        private bool _hasSelection;
        private bool _draggingScrollbar;
        private double _scrollbarGrab;

        // The scrollbar thumb from the last paint, in this element's coordinates, so a drag can hit
        // it without recomputing the layout.
        private bool _scrollbarShown;
        private double _scrollbarThumbTop;
        private double _scrollbarThumbHeight;
        private int _scrollbarRange;
        private int _anchorLine;
        private int _anchorColumn;
        private int _activeLine;
        private int _activeColumn;

        // The key that produced the text currently being delivered. Text input does not carry it,
        // and a console host asking for whole key events needs it.
        private Key _textKey = Key.None;

        public TerminalRenderer()
        {
            _visuals = new VisualCollection(this);
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;

            DefaultBackground = Color.FromRgb(0x12, 0x12, 0x14);
            DefaultForeground = Color.FromRgb(0xD6, 0xD6, 0xD2);
            SelectionBackground = Color.FromArgb(0x66, 0x3B, 0x78, 0xFF);
            CaretColor = Color.FromRgb(0xE8, 0xC0, 0x6A);

            LoadTypefaces();

            _frameTimer = new DispatcherTimer(DispatcherPriority.Render);
            _frameTimer.Interval = FrameInterval;
            _frameTimer.Tick += OnFrameTick;

            _caretTimer = new DispatcherTimer(DispatcherPriority.Background);
            _caretTimer.Interval = CaretBlinkInterval;
            _caretTimer.Tick += OnCaretTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public Color DefaultBackground { get; set; }

        public Color DefaultForeground { get; set; }

        public Color SelectionBackground { get; set; }

        public Color CaretColor { get; set; }

        public CaretShape CaretShape { get; set; }

        /// <summary>When false the caret is drawn solid instead of blinking.</summary>
        public bool CaretBlinks
        {
            get { return _caretBlinks; }

            set
            {
                if (_caretBlinks == value)
                {
                    return;
                }

                _caretBlinks = value;
                _caretOn = true;
                _fullRedraw = true;
            }
        }

        public double TerminalFontSize
        {
            get { return _fontSize; }

            set
            {
                double clamped = Math.Max(MinimumFontSize, Math.Min(MaximumFontSize, value));
                if (Math.Abs(clamped - _fontSize) < 0.01)
                {
                    return;
                }

                _fontSize = clamped;
                MeasureCell();
                _fullRedraw = true;
                UpdateTerminalSize();
            }
        }

        protected override int VisualChildrenCount
        {
            get { return _visuals.Count; }
        }

        public void Attach(ITerminalSession session, CellGrid grid)
        {
            if (_session != null)
            {
                _session.OutputReceived -= OnOutputReceived;
            }

            _session = session;
            _grid = grid;

            if (_session != null)
            {
                _session.OutputReceived += OnOutputReceived;
            }

            _scrollOffset = 0;
            _anchoredScrolledLines = _grid == null ? 0 : ReadScrolledLines(_grid);
            _fullRedraw = true;
            UpdateTerminalSize();
        }

        private static long ReadScrolledLines(CellGrid grid)
        {
            lock (grid.SyncRoot)
            {
                return grid.ScrolledLines;
            }
        }

        /// <summary>
        /// Switches the monospace face. The family string may carry fallbacks, e.g.
        /// "Cascadia Mono, Consolas"; cell metrics are re-measured from the new face.
        /// </summary>
        public void SetFontFamily(string fontFamily)
        {
            if (string.IsNullOrEmpty(fontFamily) || _fontFamily == fontFamily)
            {
                return;
            }

            _fontFamily = fontFamily;
            LoadTypefaces();
            _fullRedraw = true;
            UpdateTerminalSize();
        }

        public void Redraw()
        {
            _fullRedraw = true;
            RenderViewport();
        }

        public void PasteText(string text)
        {
            if (_session == null || _grid == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            bool bracketed;
            lock (_grid.SyncRoot)
            {
                bracketed = _grid.BracketedPaste;
            }

            ScrollToBottom();
            _session.Write(bracketed ? "\x1b[200~" + text + "\x1b[201~" : text);
        }

        public void Detach()
        {
            if (_session != null)
            {
                _session.OutputReceived -= OnOutputReceived;
                _session = null;
            }

            _grid = null;
            _frameTimer.Stop();
            _caretTimer.Stop();
        }

        public void ScrollToBottom()
        {
            if (_scrollOffset == 0)
            {
                return;
            }

            _scrollOffset = 0;
            _fullRedraw = true;
        }

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? _cellWidth * 80 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? _cellHeight * 24 : availableSize.Height;
            return new Size(width, height);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateTerminalSize();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _pixelsPerDip = (float)newDpi.PixelsPerDip;
            _fullRedraw = true;
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            _caretOn = true;
            _fullRedraw = true;
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            _caretOn = false;
            _fullRedraw = true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // A composed or input-method character has no key of its own to report.
            _textKey = e.Key == Key.ImeProcessed || e.Key == Key.DeadCharProcessed || e.Key == Key.System
                ? Key.None
                : e.Key;

            if (_session == null || _grid == null)
            {
                return;
            }

            ModifierKeys modifiers = Keyboard.Modifiers;

            if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
            {
                CopySelection();
                e.Handled = true;
                return;
            }

            if ((modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.V)
                || (modifiers == ModifierKeys.Control && e.Key == Key.V)
                || (modifiers == ModifierKeys.Shift && e.Key == Key.Insert))
            {
                Paste();
                e.Handled = true;
                return;
            }

            // Ctrl+C copies what is selected and drops the selection, so the next Ctrl+C is the
            // interrupt again. With nothing selected it falls through and interrupts as it always
            // has - the key keeps both meanings, and which one applies is on screen.
            if (modifiers == ModifierKeys.Control && e.Key == Key.C && _hasSelection)
            {
                CopySelection();
                _hasSelection = false;
                _fullRedraw = true;
                e.Handled = true;
                return;
            }

            if (modifiers == ModifierKeys.Shift && (e.Key == Key.PageUp || e.Key == Key.PageDown))
            {
                ScrollBy(e.Key == Key.PageUp ? _rows : -_rows);
                e.Handled = true;
                return;
            }

            bool applicationCursorKeys;
            bool wholeKeyEvents;
            lock (_grid.SyncRoot)
            {
                applicationCursorKeys = _grid.ApplicationCursorKeys;
                wholeKeyEvents = _grid.Win32InputMode;
            }

            string sequence = VtKeyEncoder.Encode(e.Key, modifiers, applicationCursorKeys);
            if (sequence == null)
            {
                return;
            }

            // A lone escape is the one sequence a host asking for whole key events cannot resolve,
            // because it is also how every other sequence starts. State the key instead.
            if (wholeKeyEvents && sequence == "\x1b")
            {
                sequence = VtKeyEncoder.EncodeKeyEvent(e.Key, '\x1b', modifiers);
            }

            ScrollToBottom();
            _session.Write(sequence);
            e.Handled = true;
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (_session == null || string.IsNullOrEmpty(e.Text))
            {
                return;
            }

            Key textKey = _textKey;
            _textKey = Key.None;

            bool wholeKeyEvents = false;
            if (_grid != null)
            {
                lock (_grid.SyncRoot)
                {
                    wholeKeyEvents = _grid.Win32InputMode;
                }
            }

            ScrollToBottom();
            _session.Write(wholeKeyEvents
                ? VtKeyEncoder.EncodeText(e.Text, textKey, Keyboard.Modifiers)
                : e.Text);
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            Point position = e.GetPosition(this);
            if (BeginScrollbarDrag(position))
            {
                e.Handled = true;
                return;
            }

            PointToCell(position, out _anchorLine, out _anchorColumn);
            _activeLine = _anchorLine;
            _activeColumn = _anchorColumn;
            _hasSelection = false;
            _selecting = true;
            _fullRedraw = true;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_draggingScrollbar)
            {
                ScrollToThumb(e.GetPosition(this).Y);
                return;
            }

            if (!_selecting)
            {
                return;
            }

            PointToCell(e.GetPosition(this), out _activeLine, out _activeColumn);
            _hasSelection = _activeLine != _anchorLine || _activeColumn != _anchorColumn;
            _fullRedraw = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_draggingScrollbar)
            {
                _draggingScrollbar = false;
                ReleaseMouseCapture();
                DrawScrollbar();
                return;
            }

            if (_selecting)
            {
                _selecting = false;
                ReleaseMouseCapture();
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            Paste();
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                TerminalFontSize = _fontSize + (e.Delta > 0 ? 1 : -1);
            }
            else
            {
                ScrollBy(e.Delta > 0 ? WheelLinesPerNotch : -WheelLinesPerNotch);
            }

            e.Handled = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _pixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
            _frameTimer.Start();
            _caretTimer.Start();
            _fullRedraw = true;
            UpdateTerminalSize();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _frameTimer.Stop();
            _caretTimer.Stop();
        }

        private void OnOutputReceived(object sender, TerminalOutputEventArgs e)
        {
            Interlocked.Exchange(ref _outputPending, 1);
        }

        private void OnFrameTick(object sender, EventArgs e)
        {
            bool pending = Interlocked.Exchange(ref _outputPending, 0) != 0;
            if (!pending && !_fullRedraw)
            {
                return;
            }

            RenderViewport();
        }

        private void OnCaretTick(object sender, EventArgs e)
        {
            if (!IsKeyboardFocused || !_caretBlinks)
            {
                return;
            }

            _caretOn = !_caretOn;
            InvalidateCursorRow();
            RenderViewport();
        }

        private void LoadTypefaces()
        {
            Typeface regular = new Typeface(
                new FontFamily(_fontFamily),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);

            if (!regular.TryGetGlyphTypeface(out _regularFace))
            {
                new Typeface("Consolas").TryGetGlyphTypeface(out _regularFace);
            }

            Typeface bold = new Typeface(
                new FontFamily(_fontFamily),
                FontStyles.Normal,
                FontWeights.Bold,
                FontStretches.Normal);

            if (!bold.TryGetGlyphTypeface(out _boldFace))
            {
                _boldFace = _regularFace;
            }

            MeasureCell();
        }

        private void MeasureCell()
        {
            if (_regularFace == null)
            {
                return;
            }

            ushort glyph;
            double advance = _regularFace.CharacterToGlyphMap.TryGetValue('M', out glyph)
                ? _regularFace.AdvanceWidths[glyph]
                : 0.6;

            _cellWidth = Math.Max(1, advance * _fontSize);
            _cellHeight = Math.Max(1, Math.Ceiling(_regularFace.Height * _fontSize));
            _baseline = _regularFace.Baseline * _fontSize;
        }

        private void UpdateTerminalSize()
        {
            if (_session == null || _grid == null || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            int columns = Math.Max(1, (int)(ActualWidth / _cellWidth));
            int rows = Math.Max(1, (int)(ActualHeight / _cellHeight));

            if (columns != _columns || rows != _rows)
            {
                _columns = columns;
                _rows = rows;
                _session.Resize(columns, rows);
                RebuildVisuals(rows);
                _fullRedraw = true;
            }

            RenderViewport();
        }

        private void RebuildVisuals(int rows)
        {
            // The scrollbar is one extra visual on top of the rows. Pull it out so the row-count
            // arithmetic is about rows alone, then re-append it as the last, top-most child.
            if (_visuals.Contains(_scrollbar))
            {
                _visuals.Remove(_scrollbar);
            }

            while (_visuals.Count > rows)
            {
                _visuals.RemoveAt(_visuals.Count - 1);
            }

            while (_visuals.Count < rows)
            {
                _visuals.Add(new DrawingVisual());
            }

            _visuals.Add(_scrollbar);

            _renderedVersions = new long[rows];
            for (int row = 0; row < rows; row++)
            {
                _renderedVersions[row] = -1;
            }
        }

        private void InvalidateCursorRow()
        {
            if (_grid == null || _renderedVersions == null)
            {
                return;
            }

            int row;
            lock (_grid.SyncRoot)
            {
                row = _grid.CursorRow;
            }

            if (row >= 0 && row < _renderedVersions.Length)
            {
                _renderedVersions[row] = -1;
            }
        }

        private void ScrollBy(int lines)
        {
            ScrollTo(_scrollOffset + lines);
        }

        /// <summary>
        /// Keeps a reader who has scrolled up looking at the same lines while the session keeps
        /// writing. The offset counts up from the live bottom, so without this every line that
        /// reaches the history drags the viewport a line further down and the passage being read
        /// walks off the top - which is what made the history look like it only held a screenful.
        /// Called with the grid lock held.
        /// </summary>
        private void HoldScrollPosition()
        {
            long scrolled = _grid.ScrolledLines;
            if (scrolled == _anchoredScrolledLines)
            {
                return;
            }

            if (_scrollOffset > 0)
            {
                // Never past the oldest line that can be reached: once the history is full its top
                // is genuinely gone, and the view stops there rather than pretending otherwise.
                int maximum = Math.Max(0, _grid.TotalLines - _grid.Rows - _grid.FirstUsedLine);
                long held = _scrollOffset + (scrolled - _anchoredScrolledLines);
                int offset = (int)Math.Max(0, Math.Min(maximum, held));

                if (offset != _scrollOffset)
                {
                    _scrollOffset = offset;
                    _fullRedraw = true;
                }
            }

            _anchoredScrolledLines = scrolled;
        }

        private void ScrollTo(int offset)
        {
            if (_grid == null)
            {
                return;
            }

            int maximum;
            lock (_grid.SyncRoot)
            {
                // Stop at the first line that has anything on it, not at the start of the history.
                // Blank lines get into the history whenever the pane is made shorter, and scrolling
                // up into that emptiness - past the banner the session opened with - looks broken.
                maximum = Math.Max(0, _grid.TotalLines - _grid.Rows - _grid.FirstUsedLine);
            }

            offset = Math.Max(0, Math.Min(maximum, offset));
            if (offset == _scrollOffset)
            {
                return;
            }

            _scrollOffset = offset;
            _fullRedraw = true;
            RenderViewport();
        }

        private bool BeginScrollbarDrag(Point position)
        {
            if (!_scrollbarShown || position.X < ActualWidth - ScrollbarLaneWidth)
            {
                return false;
            }

            // On the thumb, keep the pointer's grip within it; on the lane elsewhere, jump the thumb
            // under the pointer and carry on dragging from its middle.
            if (position.Y >= _scrollbarThumbTop && position.Y < _scrollbarThumbTop + _scrollbarThumbHeight)
            {
                _scrollbarGrab = position.Y - _scrollbarThumbTop;
            }
            else
            {
                _scrollbarGrab = _scrollbarThumbHeight / 2;
            }

            _draggingScrollbar = true;
            CaptureMouse();
            ScrollToThumb(position.Y);
            DrawScrollbar();
            return true;
        }

        private void ScrollToThumb(double pointerY)
        {
            double travel = ActualHeight - _scrollbarThumbHeight;
            if (travel <= 0 || _scrollbarRange <= 0)
            {
                return;
            }

            double thumbTop = Math.Max(0, Math.Min(travel, pointerY - _scrollbarGrab));

            // The top of the track is the oldest reachable line, the bottom is the live output.
            double fraction = 1 - (thumbTop / travel);
            ScrollTo((int)Math.Round(fraction * _scrollbarRange));
        }

        private void RenderViewport()
        {
            if (_grid == null || _regularFace == null)
            {
                return;
            }

            lock (_grid.SyncRoot)
            {
                HoldScrollPosition();

                int rows = _grid.Rows;
                if (_visuals.Count != rows + 1 || _renderedVersions == null || _renderedVersions.Length != rows)
                {
                    RebuildVisuals(rows);
                    _fullRedraw = true;
                }

                int total = _grid.TotalLines;
                int top = Math.Max(0, total - rows - _scrollOffset);
                int cursorRow = _grid.CursorRow;
                bool cursorVisible = _grid.CursorVisible && IsKeyboardFocused && _caretOn && _scrollOffset == 0;

                for (int row = 0; row < rows; row++)
                {
                    int absolute = top + row;
                    TerminalCell[] cells;
                    long version;
                    if (!_grid.TryGetLine(absolute, out cells, out version))
                    {
                        continue;
                    }

                    bool carriesCursor = cursorVisible && row == cursorRow;
                    if (!_fullRedraw && !carriesCursor && _renderedVersions[row] == version)
                    {
                        continue;
                    }

                    DrawRow((DrawingVisual)_visuals[row], cells, absolute, row, carriesCursor ? _grid.CursorColumn : -1);
                    _renderedVersions[row] = _fullRedraw || carriesCursor ? -1 : version;
                }

                _fullRedraw = false;
            }

            DrawScrollbar();
        }

        private void DrawScrollbar()
        {
            double height = ActualHeight;
            double width = ActualWidth;

            int rows;
            int range;
            int offset = _scrollOffset;
            if (_grid == null)
            {
                rows = 0;
                range = 0;
            }
            else
            {
                lock (_grid.SyncRoot)
                {
                    rows = _grid.Rows;
                    range = Math.Max(0, _grid.TotalLines - rows - _grid.FirstUsedLine);
                }
            }

            _scrollbarRange = range;

            if (range <= 0 || rows <= 0 || height <= 0 || width <= 0)
            {
                _scrollbarShown = false;
                using (_scrollbar.RenderOpen())
                {
                }

                return;
            }

            double usable = rows + range;
            double thumbHeight = Math.Max(ScrollbarMinThumb, height * rows / usable);
            double travel = height - thumbHeight;

            // Offset 0 is the live bottom, so the thumb sits at the bottom then; the top of the
            // track is the oldest line that can be reached.
            double fraction = (double)offset / range;
            double thumbTop = (1 - fraction) * travel;

            _scrollbarShown = true;
            _scrollbarThumbTop = thumbTop;
            _scrollbarThumbHeight = thumbHeight;

            double thumbLeft = width - ((ScrollbarLaneWidth + ScrollbarThumbWidth) / 2);
            double radius = ScrollbarThumbWidth / 2;

            Color fg = DefaultForeground;
            byte thumbAlpha = (byte)(_draggingScrollbar ? 0x9A : (offset > 0 ? 0x62 : 0x38));
            Brush thumb = new SolidColorBrush(Color.FromArgb(thumbAlpha, fg.R, fg.G, fg.B));
            thumb.Freeze();
            Brush track = new SolidColorBrush(Color.FromArgb(0x12, fg.R, fg.G, fg.B));
            track.Freeze();

            using (DrawingContext context = _scrollbar.RenderOpen())
            {
                context.DrawRoundedRectangle(track, null,
                    new Rect(thumbLeft, 0, ScrollbarThumbWidth, height), radius, radius);
                context.DrawRoundedRectangle(thumb, null,
                    new Rect(thumbLeft, thumbTop, ScrollbarThumbWidth, thumbHeight), radius, radius);
            }
        }

        private void DrawRow(DrawingVisual visual, TerminalCell[] cells, int absoluteLine, int viewportRow, int cursorColumn)
        {
            int selectionStart;
            int selectionEnd;
            GetSelectionRange(absoluteLine, out selectionStart, out selectionEnd);

            using (DrawingContext context = visual.RenderOpen())
            {
                double y = viewportRow * _cellHeight;
                context.DrawRectangle(GetBrush(DefaultBackground), null, new Rect(0, y, Math.Max(ActualWidth, 1), _cellHeight));

                int column = 0;
                while (column < cells.Length)
                {
                    int runStart = column;
                    TerminalCell first = cells[column];
                    bool firstSelected = column >= selectionStart && column < selectionEnd;

                    while (column < cells.Length
                           && cells[column].SameAttributes(first)
                           && (column >= selectionStart && column < selectionEnd) == firstSelected)
                    {
                        column++;
                    }

                    DrawRun(context, cells, runStart, column - runStart, y, firstSelected);
                }

                if (cursorColumn >= 0 && cursorColumn < cells.Length)
                {
                    DrawCaret(context, cells, cursorColumn, y);
                }
            }
        }

        private void DrawRun(DrawingContext context, TerminalCell[] cells, int start, int length, double y, bool selected)
        {
            TerminalCell attributes = cells[start];
            int foregroundValue = attributes.Foreground;
            int backgroundValue = attributes.Background;

            if ((attributes.Flags & CellFlags.Inverse) != 0)
            {
                int swap = foregroundValue;
                foregroundValue = backgroundValue == 0 ? ColorToArgb(DefaultBackground) : backgroundValue;
                backgroundValue = swap == 0 ? ColorToArgb(DefaultForeground) : swap;
            }

            Color foreground = foregroundValue == 0 ? DefaultForeground : ArgbToColor(foregroundValue);
            double x = start * _cellWidth;
            double width = length * _cellWidth;

            if (selected)
            {
                context.DrawRectangle(GetBrush(SelectionBackground), null, new Rect(x, y, width, _cellHeight));
            }
            else if (backgroundValue != 0)
            {
                context.DrawRectangle(GetBrush(ArgbToColor(backgroundValue)), null, new Rect(x, y, width, _cellHeight));
            }

            if ((attributes.Flags & CellFlags.Hidden) != 0)
            {
                return;
            }

            GlyphTypeface face = (attributes.Flags & CellFlags.Bold) != 0 ? _boldFace : _regularFace;
            GlyphRun run = BuildGlyphRun(face, cells, start, length, x, y);
            if (run != null)
            {
                context.DrawGlyphRun(GetBrush(foreground), run);
            }

            if ((attributes.Flags & CellFlags.Underline) != 0)
            {
                double underlineY = Math.Floor(y + _baseline + 1.5) + 0.5;
                context.DrawLine(new Pen(GetBrush(foreground), 1), new Point(x, underlineY), new Point(x + width, underlineY));
            }
        }

        private void DrawCaret(DrawingContext context, TerminalCell[] cells, int column, double y)
        {
            double x = column * _cellWidth;
            Brush caret = GetBrush(CaretColor);

            if (CaretShape == CaretShape.Bar)
            {
                context.DrawRectangle(caret, null, new Rect(x, y, Math.Max(1, _cellWidth * 0.15), _cellHeight));
                return;
            }

            if (CaretShape == CaretShape.Underline)
            {
                double thickness = Math.Max(1, _cellHeight * 0.1);
                context.DrawRectangle(caret, null, new Rect(x, y + _cellHeight - thickness, _cellWidth, thickness));
                return;
            }

            context.DrawRectangle(caret, null, new Rect(x, y, _cellWidth, _cellHeight));

            GlyphRun run = BuildGlyphRun(_regularFace, cells, column, 1, x, y);
            if (run != null)
            {
                context.DrawGlyphRun(GetBrush(DefaultBackground), run);
            }
        }

        private GlyphRun BuildGlyphRun(GlyphTypeface face, TerminalCell[] cells, int start, int length, double x, double y)
        {
            ushort[] indices = new ushort[length];
            double[] advances = new double[length];
            char[] characters = new char[length];
            bool anyVisible = false;

            for (int i = 0; i < length; i++)
            {
                char character = cells[start + i].Character;
                if (character == '\0')
                {
                    character = ' ';
                }

                ushort glyph;
                if (!face.CharacterToGlyphMap.TryGetValue(character, out glyph))
                {
                    face.CharacterToGlyphMap.TryGetValue(' ', out glyph);
                    character = ' ';
                }

                if (character != ' ')
                {
                    anyVisible = true;
                }

                indices[i] = glyph;
                advances[i] = _cellWidth;
                characters[i] = character;
            }

            if (!anyVisible)
            {
                return null;
            }

            return new GlyphRun(
                face,
                0,
                false,
                _fontSize,
                _pixelsPerDip,
                indices,
                new Point(x, y + _baseline),
                advances,
                null,
                characters,
                null,
                null,
                null,
                null);
        }

        private void GetSelectionRange(int absoluteLine, out int start, out int end)
        {
            start = 0;
            end = 0;

            if (!_hasSelection)
            {
                return;
            }

            int firstLine = _anchorLine;
            int firstColumn = _anchorColumn;
            int lastLine = _activeLine;
            int lastColumn = _activeColumn;

            if (firstLine > lastLine || (firstLine == lastLine && firstColumn > lastColumn))
            {
                int line = firstLine;
                int column = firstColumn;
                firstLine = lastLine;
                firstColumn = lastColumn;
                lastLine = line;
                lastColumn = column;
            }

            if (absoluteLine < firstLine || absoluteLine > lastLine)
            {
                return;
            }

            start = absoluteLine == firstLine ? firstColumn : 0;
            end = absoluteLine == lastLine ? lastColumn : _grid.Columns;
        }

        private void PointToCell(Point point, out int line, out int column)
        {
            int rows = _grid == null ? 1 : _grid.Rows;
            int total = _grid == null ? rows : _grid.TotalLines;
            int top = Math.Max(0, total - rows - _scrollOffset);

            int row = (int)(point.Y / _cellHeight);
            row = Math.Max(0, Math.Min(rows - 1, row));
            line = top + row;

            column = (int)Math.Round(point.X / _cellWidth);
            column = Math.Max(0, Math.Min(_grid == null ? 0 : _grid.Columns, column));
        }

        private void CopySelection()
        {
            if (!_hasSelection || _grid == null)
            {
                return;
            }

            StringBuilder text = new StringBuilder();

            lock (_grid.SyncRoot)
            {
                int firstLine = Math.Min(_anchorLine, _activeLine);
                int lastLine = Math.Max(_anchorLine, _activeLine);

                for (int line = firstLine; line <= lastLine; line++)
                {
                    TerminalCell[] cells;
                    long version;
                    if (!_grid.TryGetLine(line, out cells, out version))
                    {
                        continue;
                    }

                    int start;
                    int end;
                    GetSelectionRange(line, out start, out end);

                    StringBuilder lineText = new StringBuilder();
                    for (int column = start; column < Math.Min(end, cells.Length); column++)
                    {
                        lineText.Append(cells[column].Character == '\0' ? ' ' : cells[column].Character);
                    }

                    text.Append(lineText.ToString().TrimEnd());
                    if (line != lastLine)
                    {
                        text.Append(Environment.NewLine);
                    }
                }
            }

            if (text.Length > 0)
            {
                Clipboard.SetText(text.ToString());
            }
        }

        /// <summary>
        /// Text on the clipboard is typed into the session. A picture has no text form, so the
        /// keystroke itself is handed over instead: a command line program that takes a pasted
        /// picture reads the clipboard for itself, and it can only know to do that if it is told the
        /// key was pressed. Swallowing the key here is what made a captured screenshot impossible to
        /// paste into one.
        /// </summary>
        private void Paste()
        {
            if (_session == null)
            {
                return;
            }

            string text = null;
            bool picture = false;

            try
            {
                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }
                else
                {
                    picture = Clipboard.ContainsImage();
                }
            }
            catch (ExternalException)
            {
                // Another application had the clipboard open. Nothing to paste this time.
                return;
            }

            if (!string.IsNullOrEmpty(text))
            {
                PasteText(text.Replace("\r\n", "\r").Replace("\n", "\r"));
                return;
            }

            if (picture)
            {
                ForwardPasteKey();
            }
        }

        private void ForwardPasteKey()
        {
            bool wholeKeyEvents = false;
            if (_grid != null)
            {
                lock (_grid.SyncRoot)
                {
                    wholeKeyEvents = _grid.Win32InputMode;
                }
            }

            ScrollToBottom();
            _session.Write(wholeKeyEvents
                ? VtKeyEncoder.EncodeKeyEvent(Key.V, PasteControlCharacter, ModifierKeys.Control)
                : PasteControlCharacter.ToString());
        }

        private Brush GetBrush(Color color)
        {
            int key = ColorToArgb(color);

            Brush brush;
            if (_brushes.TryGetValue(key, out brush))
            {
                return brush;
            }

            SolidColorBrush created = new SolidColorBrush(color);
            created.Freeze();
            _brushes.Add(key, created);
            return created;
        }

        private static int ColorToArgb(Color color)
        {
            return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        private static Color ArgbToColor(int argb)
        {
            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }
    }
}
