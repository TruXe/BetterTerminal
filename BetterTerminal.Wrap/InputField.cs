using System;
using System.Text;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// One editable line. It is not Console.ReadLine: that one owns the screen while it waits,
    /// which a full-screen interface cannot allow. This keeps the text, the caret and the part of
    /// it that is on screen, and the loop stays free to redraw or to notice a child exiting.
    /// </summary>
    public sealed class InputField
    {
        private readonly StringBuilder _text = new StringBuilder();

        private int _caret;
        private int _offset;

        public InputField(string hint)
        {
            Hint = hint;
        }

        /// <summary>Shown in place of the text while the field is empty.</summary>
        public string Hint { get; private set; }

        public string Value
        {
            get { return _text.ToString(); }
        }

        public bool IsEmpty
        {
            get { return _text.Length == 0; }
        }

        /// <summary>Returns false for a key the field does not use, so the screen can act on it.</summary>
        public bool HandleKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    _caret = Math.Max(0, _caret - 1);
                    return true;

                case ConsoleKey.RightArrow:
                    _caret = Math.Min(_text.Length, _caret + 1);
                    return true;

                case ConsoleKey.Home:
                    _caret = 0;
                    return true;

                case ConsoleKey.End:
                    _caret = _text.Length;
                    return true;

                case ConsoleKey.Backspace:
                    if (_caret > 0)
                    {
                        _text.Remove(_caret - 1, 1);
                        _caret--;
                    }

                    return true;

                case ConsoleKey.Delete:
                    if (_caret < _text.Length)
                    {
                        _text.Remove(_caret, 1);
                    }

                    return true;

                default:
                    if (char.IsControl(key.KeyChar))
                    {
                        return false;
                    }

                    _text.Insert(_caret, key.KeyChar);
                    _caret++;
                    return true;
            }
        }

        /// <summary>
        /// Draws the field as a filled bar: an accent edge when it has the focus, the text, and
        /// the caret as one inverted cell. A value longer than the bar scrolls under the caret
        /// rather than being cut, so the end of a long path stays visible while it is typed.
        /// </summary>
        public void Render(AnsiWriter writer, int row, int column, int width, bool focused)
        {
            if (width < 4)
            {
                return;
            }

            int inner = width - 2;
            KeepCaretVisible(inner);

            writer.FocusEdge(row, column, focused ? Palette.Accent : Palette.StrokeDefault, Palette.Elevated);
            writer.Fill(row, column + 1, width - 1, Palette.Elevated);
            writer.MoveTo(row, column + 2);

            if (_text.Length == 0 && !focused)
            {
                writer.Foreground(Palette.TextTertiary);
                writer.WriteClipped(Hint, inner - 1);
                return;
            }

            string visible = Visible(inner);
            writer.Foreground(Palette.TextPrimary);
            writer.Write(visible);

            if (!focused)
            {
                return;
            }

            int caretColumn = column + 2 + (_caret - _offset);
            writer.MoveTo(row, caretColumn);
            writer.Background(Palette.Accent);
            writer.Foreground(Palette.AccentInk);
            writer.Write(_caret < _text.Length ? _text[_caret].ToString() : " ");
        }

        private string Visible(int inner)
        {
            int length = Math.Min(inner - 1, _text.Length - _offset);
            return length <= 0 ? string.Empty : _text.ToString(_offset, length);
        }

        private void KeepCaretVisible(int inner)
        {
            int room = Math.Max(1, inner - 1);

            if (_caret < _offset)
            {
                _offset = _caret;
            }
            else if (_caret - _offset >= room)
            {
                _offset = _caret - room + 1;
            }

            if (_offset > _text.Length)
            {
                _offset = _text.Length;
            }
        }
    }
}
