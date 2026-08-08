using System;
using System.Collections.Generic;
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

        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);
        private static readonly TimeSpan CaretBlinkInterval = TimeSpan.FromMilliseconds(530);

        private readonly VisualCollection _visuals;
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
        private int _columns;
        private int _rows;
        private bool _fullRedraw = true;
        private bool _caretOn = true;
        private bool _caretBlinks = true;
        private bool _selecting;
        private bool _hasSelection;
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
            _fullRedraw = true;
            UpdateTerminalSize();
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

            PointToCell(e.GetPosition(this), out _anchorLine, out _anchorColumn);
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
            while (_visuals.Count > rows)
            {
                _visuals.RemoveAt(_visuals.Count - 1);
            }

            while (_visuals.Count < rows)
            {
                _visuals.Add(new DrawingVisual());
            }

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
            if (_grid == null)
            {
                return;
            }

            int maximum;
            lock (_grid.SyncRoot)
            {
                maximum = Math.Max(0, _grid.TotalLines - _grid.Rows);
            }

            int offset = Math.Max(0, Math.Min(maximum, _scrollOffset + lines));
            if (offset == _scrollOffset)
            {
                return;
            }

            _scrollOffset = offset;
            _fullRedraw = true;
            RenderViewport();
        }

        private void RenderViewport()
        {
            if (_grid == null || _regularFace == null)
            {
                return;
            }

            lock (_grid.SyncRoot)
            {
                int rows = _grid.Rows;
                if (_visuals.Count != rows || _renderedVersions == null || _renderedVersions.Length != rows)
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

        private void Paste()
        {
            if (_session == null || !Clipboard.ContainsText())
            {
                return;
            }

            PasteText(Clipboard.GetText().Replace("\r\n", "\r").Replace("\n", "\r"));
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
