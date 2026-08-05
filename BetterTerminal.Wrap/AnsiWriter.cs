using System;
using System.Text;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// Builds one frame in memory and writes it in a single call. Writing escape sequences
    /// straight to the console flushes per call and makes a full redraw visibly tear.
    ///
    /// Every drawing primitive the screens need lives here, so no screen ever writes an escape
    /// sequence of its own.
    /// </summary>
    public sealed class AnsiWriter
    {
        private const char Escape = '\u001b';

        // Box drawing, written as escapes: this file stays plain ASCII, because the compiler only
        // reads it as UTF-8 when it carries a byte order mark.
        private const char Horizontal = '\u2500';
        private const char Vertical = '\u2502';
        private const char TopLeft = '\u250c';
        private const char TopRight = '\u2510';
        private const char BottomLeft = '\u2514';
        private const char BottomRight = '\u2518';
        private const char LeftEdge = '\u258c';
        private const char TrackLight = '\u2591';
        private const char TrackHeavy = '\u2588';

        private readonly StringBuilder _frame = new StringBuilder(16384);

        public void EnterAlternateScreen()
        {
            _frame.Append(Escape).Append("[?1049h");
        }

        public void LeaveAlternateScreen()
        {
            _frame.Append(Escape).Append("[?1049l");
        }

        public void HideCursor()
        {
            _frame.Append(Escape).Append("[?25l");
        }

        public void ShowCursor()
        {
            _frame.Append(Escape).Append("[?25h");
        }

        /// <summary>Clears with the background currently set, which is how the page gets its colour.</summary>
        public void Clear()
        {
            _frame.Append(Escape).Append("[2J");
        }

        /// <summary>Rows and columns are zero-based here; the sequence itself is one-based.</summary>
        public void MoveTo(int row, int column)
        {
            _frame.Append(Escape).Append('[').Append(row + 1).Append(';').Append(column + 1).Append('H');
        }

        public void Foreground(TerminalColor colour)
        {
            _frame.Append(Escape).Append("[38;2;")
                .Append(colour.R).Append(';').Append(colour.G).Append(';').Append(colour.B).Append('m');
        }

        public void Background(TerminalColor colour)
        {
            _frame.Append(Escape).Append("[48;2;")
                .Append(colour.R).Append(';').Append(colour.G).Append(';').Append(colour.B).Append('m');
        }

        public void ResetAttributes()
        {
            _frame.Append(Escape).Append("[0m");
        }

        public void Write(string text)
        {
            _frame.Append(text);
        }

        public void Write(char character, int count)
        {
            if (count > 0)
            {
                _frame.Append(character, count);
            }
        }

        /// <summary>Writes at most <paramref name="width"/> columns, ending in an ellipsis rather
        /// than cutting a word off mid-air when the text does not fit.</summary>
        public void WriteClipped(string text, int width)
        {
            if (width <= 0 || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (text.Length <= width)
            {
                _frame.Append(text);
                return;
            }

            _frame.Append(width <= 3 ? text.Substring(0, width) : text.Substring(0, width - 3) + "...");
        }

        public void Fill(int row, int column, int width, TerminalColor background)
        {
            if (width <= 0)
            {
                return;
            }

            MoveTo(row, column);
            Background(background);
            _frame.Append(' ', width);
        }

        /// <summary>
        /// A filled panel with a one-cell border and an optional title sitting in the top edge.
        /// The caller draws inside it starting one row and two columns in.
        /// </summary>
        public void Panel(int row, int column, int width, int height,
            TerminalColor border, TerminalColor background, string title, TerminalColor titleColour)
        {
            if (width < 2 || height < 2)
            {
                return;
            }

            Background(background);
            Foreground(border);

            MoveTo(row, column);
            _frame.Append(TopLeft).Append(Horizontal, width - 2).Append(TopRight);

            for (int line = 1; line < height - 1; line++)
            {
                MoveTo(row + line, column);
                _frame.Append(Vertical);
                _frame.Append(' ', width - 2);
                _frame.Append(Vertical);
            }

            MoveTo(row + height - 1, column);
            _frame.Append(BottomLeft).Append(Horizontal, width - 2).Append(BottomRight);

            if (string.IsNullOrEmpty(title) || width < 8)
            {
                return;
            }

            MoveTo(row, column + 2);
            Foreground(titleColour);
            _frame.Append(' ');
            WriteClipped(title, width - 6);
            _frame.Append(' ');
        }

        /// <summary>The focus edge of an input row: a half block in the accent colour.</summary>
        public void FocusEdge(int row, int column, TerminalColor colour, TerminalColor background)
        {
            MoveTo(row, column);
            Background(background);
            Foreground(colour);
            _frame.Append(LeftEdge);
        }

        /// <summary>
        /// A vertical scroll indicator: the thumb covers the visible share of the content, so a
        /// long run reads as a short thumb without any number having to be printed.
        /// </summary>
        public void ScrollBar(int row, int column, int height, int first, int visible, int total,
            TerminalColor track, TerminalColor thumb, TerminalColor background)
        {
            if (height <= 0 || total <= visible || visible <= 0)
            {
                return;
            }

            int thumbHeight = Math.Max(1, visible * height / total);
            int span = Math.Max(1, total - visible);
            int thumbTop = (height - thumbHeight) * Math.Min(first, span) / span;

            Background(background);

            for (int line = 0; line < height; line++)
            {
                MoveTo(row + line, column);
                bool inThumb = line >= thumbTop && line < thumbTop + thumbHeight;
                Foreground(inThumb ? thumb : track);
                _frame.Append(inThumb ? TrackHeavy : TrackLight);
            }
        }

        public void Flush()
        {
            if (_frame.Length == 0)
            {
                return;
            }

            Console.Out.Write(_frame.ToString());
            Console.Out.Flush();
            _frame.Clear();
        }
    }
}
