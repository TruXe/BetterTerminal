using System;
using System.Text;

namespace BetterTerminal.Terminal
{
    public sealed class VtParser
    {
        private const int MaxParameters = 16;
        private const char Bell = '\a';
        private const char Escape = '\x1b';

        private readonly CellGrid _grid;
        private readonly int[] _parameters = new int[MaxParameters];
        private readonly StringBuilder _stringBuffer = new StringBuilder();

        private State _state;
        private int _parameterCount;
        private char _privateMarker;
        private bool _stringIsOsc;

        public VtParser(CellGrid grid)
        {
            _grid = grid;
        }

        private enum State
        {
            Ground,
            Escape,
            CsiParameters,
            StringPayload,
            StringEscape,
            CharsetSelector
        }

        public event EventHandler<TerminalTitleEventArgs> TitleChanged;

        // Answerback for DSR and DA queries; the session wires this to its input pipe.
        public Action<string> ResponseWriter { get; set; }

        public void Parse(char[] buffer, int count)
        {
            lock (_grid.SyncRoot)
            {
                for (int i = 0; i < count; i++)
                {
                    Consume(buffer[i]);
                }
            }
        }

        private void Consume(char c)
        {
            switch (_state)
            {
                case State.Ground:
                    ConsumeGround(c);
                    break;

                case State.Escape:
                    ConsumeEscape(c);
                    break;

                case State.CsiParameters:
                    ConsumeCsi(c);
                    break;

                case State.StringPayload:
                    ConsumeString(c);
                    break;

                case State.StringEscape:
                    if (c == '\\')
                    {
                        FinishString();
                    }
                    else
                    {
                        _stringBuffer.Append(Escape);
                        _state = State.StringPayload;
                        ConsumeString(c);
                    }

                    break;

                case State.CharsetSelector:
                    _state = State.Ground;
                    break;
            }
        }

        private void ConsumeGround(char c)
        {
            switch (c)
            {
                case Escape:
                    _state = State.Escape;
                    break;

                case '\r':
                    _grid.CarriageReturn();
                    break;

                case '\n':
                case '\v':
                case '\f':
                    _grid.LineFeed();
                    break;

                case '\b':
                    _grid.Backspace();
                    break;

                case '\t':
                    _grid.Tab();
                    break;

                case Bell:
                    break;

                default:
                    if (c >= ' ' && c != '\x7f')
                    {
                        _grid.Write(c);
                    }

                    break;
            }
        }

        private void ConsumeEscape(char c)
        {
            switch (c)
            {
                case '[':
                    ResetParameters();
                    _state = State.CsiParameters;
                    break;

                case ']':
                    _stringBuffer.Length = 0;
                    _stringIsOsc = true;
                    _state = State.StringPayload;
                    break;

                case 'P':
                case '^':
                case '_':
                    _stringBuffer.Length = 0;
                    _stringIsOsc = false;
                    _state = State.StringPayload;
                    break;

                case '(':
                case ')':
                case '*':
                case '+':
                    _state = State.CharsetSelector;
                    break;

                case 'D':
                    _grid.LineFeed();
                    _state = State.Ground;
                    break;

                case 'E':
                    _grid.CarriageReturn();
                    _grid.LineFeed();
                    _state = State.Ground;
                    break;

                case 'M':
                    _grid.ReverseLineFeed();
                    _state = State.Ground;
                    break;

                case '7':
                    _grid.SaveCursor();
                    _state = State.Ground;
                    break;

                case '8':
                    _grid.RestoreCursor();
                    _state = State.Ground;
                    break;

                case 'c':
                    _grid.ResetAttributes();
                    _grid.EraseInDisplay(2);
                    _grid.SetCursor(0, 0);
                    _state = State.Ground;
                    break;

                default:
                    _state = State.Ground;
                    break;
            }
        }

        private void ConsumeCsi(char c)
        {
            if (c >= '0' && c <= '9')
            {
                if (_parameterCount == 0)
                {
                    _parameterCount = 1;
                }

                _parameters[_parameterCount - 1] = (_parameters[_parameterCount - 1] * 10) + (c - '0');
                return;
            }

            if (c == ';')
            {
                if (_parameterCount < MaxParameters)
                {
                    _parameterCount++;
                }

                return;
            }

            if (c == '?' || c == '<' || c == '=' || c == '>')
            {
                _privateMarker = c;
                return;
            }

            if (c >= ' ' && c <= '/')
            {
                // Intermediate bytes carry no meaning for the sequences this terminal implements.
                return;
            }

            if (c >= '@' && c <= '~')
            {
                Dispatch(c);
                _state = State.Ground;
                return;
            }

            _state = State.Ground;
        }

        private void ConsumeString(char c)
        {
            if (c == Bell)
            {
                FinishString();
                return;
            }

            if (c == Escape)
            {
                _state = State.StringEscape;
                return;
            }

            if (_stringBuffer.Length < 1024)
            {
                _stringBuffer.Append(c);
            }
        }

        private void FinishString()
        {
            if (_stringIsOsc)
            {
                string payload = _stringBuffer.ToString();
                int separator = payload.IndexOf(';');
                if (separator > 0)
                {
                    string command = payload.Substring(0, separator);
                    if (command == "0" || command == "2")
                    {
                        RaiseTitleChanged(payload.Substring(separator + 1));
                    }
                }
            }

            _stringBuffer.Length = 0;
            _state = State.Ground;
        }

        private void Dispatch(char final)
        {
            switch (final)
            {
                case 'A':
                    _grid.MoveCursor(0, -Parameter(0, 1));
                    break;

                case 'B':
                    _grid.MoveCursor(0, Parameter(0, 1));
                    break;

                case 'C':
                    _grid.MoveCursor(Parameter(0, 1), 0);
                    break;

                case 'D':
                    _grid.MoveCursor(-Parameter(0, 1), 0);
                    break;

                case 'E':
                    _grid.SetCursor(0, _grid.CursorRow + Parameter(0, 1));
                    break;

                case 'F':
                    _grid.SetCursor(0, _grid.CursorRow - Parameter(0, 1));
                    break;

                case 'G':
                case '`':
                    _grid.SetCursor(Parameter(0, 1) - 1, _grid.CursorRow);
                    break;

                case 'd':
                    _grid.SetCursor(_grid.CursorColumn, Parameter(0, 1) - 1);
                    break;

                case 'H':
                case 'f':
                    _grid.SetCursor(Parameter(1, 1) - 1, Parameter(0, 1) - 1);
                    break;

                case 'J':
                    _grid.EraseInDisplay(Parameter(0, 0));
                    break;

                case 'K':
                    _grid.EraseInLine(Parameter(0, 0));
                    break;

                case 'L':
                    _grid.InsertLines(Parameter(0, 1));
                    break;

                case 'M':
                    _grid.DeleteLines(Parameter(0, 1));
                    break;

                case 'P':
                    _grid.DeleteCharacters(Parameter(0, 1));
                    break;

                case '@':
                    _grid.InsertCharacters(Parameter(0, 1));
                    break;

                case 'X':
                    _grid.EraseCharacters(Parameter(0, 1));
                    break;

                case 'S':
                    _grid.ScrollUp(Parameter(0, 1));
                    break;

                case 'T':
                    _grid.ScrollDown(Parameter(0, 1));
                    break;

                case 'r':
                    _grid.SetScrollRegion(Parameter(0, 1) - 1, Parameter(1, _grid.Rows) - 1);
                    break;

                case 's':
                    _grid.SaveCursor();
                    break;

                case 'u':
                    _grid.RestoreCursor();
                    break;

                case 'm':
                    ApplyGraphicRendition();
                    break;

                case 'h':
                    SetMode(true);
                    break;

                case 'l':
                    SetMode(false);
                    break;

                case 'n':
                    if (Parameter(0, 0) == 6)
                    {
                        WriteResponse("\x1b[" + (_grid.CursorRow + 1) + ";" + (_grid.CursorColumn + 1) + "R");
                    }

                    break;

                case 'c':
                    WriteResponse("\x1b[?1;0c");
                    break;
            }
        }

        private void SetMode(bool enabled)
        {
            if (_privateMarker != '?')
            {
                return;
            }

            for (int i = 0; i < Math.Max(1, _parameterCount); i++)
            {
                switch (_parameters[i])
                {
                    case 1:
                        _grid.ApplicationCursorKeys = enabled;
                        break;

                    case 7:
                        _grid.AutoWrap = enabled;
                        break;

                    case 25:
                        _grid.CursorVisible = enabled;
                        break;

                    case 1048:
                        if (enabled)
                        {
                            _grid.SaveCursor();
                        }
                        else
                        {
                            _grid.RestoreCursor();
                        }

                        break;

                    case 47:
                    case 1047:
                    case 1049:
                        if (enabled)
                        {
                            _grid.SaveCursor();
                            _grid.EnterAlternateScreen();
                        }
                        else
                        {
                            _grid.LeaveAlternateScreen();
                            _grid.RestoreCursor();
                        }

                        break;

                    case 2004:
                        _grid.BracketedPaste = enabled;
                        break;
                }
            }
        }

        private void ApplyGraphicRendition()
        {
            if (_parameterCount == 0)
            {
                _grid.ResetAttributes();
                return;
            }

            for (int i = 0; i < _parameterCount; i++)
            {
                int code = _parameters[i];

                if (code == 38 || code == 48)
                {
                    int color;
                    int consumed = ReadExtendedColor(i, out color);
                    if (consumed == 0)
                    {
                        return;
                    }

                    if (code == 38)
                    {
                        _grid.CurrentForeground = color;
                    }
                    else
                    {
                        _grid.CurrentBackground = color;
                    }

                    i += consumed;
                    continue;
                }

                ApplySimpleRendition(code);
            }
        }

        private void ApplySimpleRendition(int code)
        {
            if (code == 0)
            {
                _grid.ResetAttributes();
            }
            else if (code == 1)
            {
                _grid.CurrentFlags |= CellFlags.Bold;
            }
            else if (code == 2)
            {
                _grid.CurrentFlags |= CellFlags.Dim;
            }
            else if (code == 3)
            {
                _grid.CurrentFlags |= CellFlags.Italic;
            }
            else if (code == 4)
            {
                _grid.CurrentFlags |= CellFlags.Underline;
            }
            else if (code == 7)
            {
                _grid.CurrentFlags |= CellFlags.Inverse;
            }
            else if (code == 8)
            {
                _grid.CurrentFlags |= CellFlags.Hidden;
            }
            else if (code == 22)
            {
                _grid.CurrentFlags &= ~(CellFlags.Bold | CellFlags.Dim);
            }
            else if (code == 23)
            {
                _grid.CurrentFlags &= ~CellFlags.Italic;
            }
            else if (code == 24)
            {
                _grid.CurrentFlags &= ~CellFlags.Underline;
            }
            else if (code == 27)
            {
                _grid.CurrentFlags &= ~CellFlags.Inverse;
            }
            else if (code == 28)
            {
                _grid.CurrentFlags &= ~CellFlags.Hidden;
            }
            else if (code >= 30 && code <= 37)
            {
                _grid.CurrentForeground = TerminalPalette.Get(code - 30);
            }
            else if (code == 39)
            {
                _grid.CurrentForeground = 0;
            }
            else if (code >= 40 && code <= 47)
            {
                _grid.CurrentBackground = TerminalPalette.Get(code - 40);
            }
            else if (code == 49)
            {
                _grid.CurrentBackground = 0;
            }
            else if (code >= 90 && code <= 97)
            {
                _grid.CurrentForeground = TerminalPalette.Get(code - 90 + 8);
            }
            else if (code >= 100 && code <= 107)
            {
                _grid.CurrentBackground = TerminalPalette.Get(code - 100 + 8);
            }
        }

        private int ReadExtendedColor(int index, out int color)
        {
            color = 0;
            if (index + 1 >= _parameterCount)
            {
                return 0;
            }

            int mode = _parameters[index + 1];
            if (mode == 5 && index + 2 < _parameterCount)
            {
                color = TerminalPalette.Get(_parameters[index + 2]);
                return 2;
            }

            if (mode == 2 && index + 4 < _parameterCount)
            {
                color = TerminalPalette.FromRgb(
                    _parameters[index + 2],
                    _parameters[index + 3],
                    _parameters[index + 4]);
                return 4;
            }

            return 0;
        }

        private int Parameter(int index, int fallback)
        {
            if (index >= _parameterCount)
            {
                return fallback;
            }

            int value = _parameters[index];
            return value == 0 && fallback != 0 ? fallback : value;
        }

        private void ResetParameters()
        {
            for (int i = 0; i < MaxParameters; i++)
            {
                _parameters[i] = 0;
            }

            _parameterCount = 0;
            _privateMarker = '\0';
        }

        private void WriteResponse(string response)
        {
            Action<string> writer = ResponseWriter;
            if (writer != null)
            {
                writer(response);
            }
        }

        private void RaiseTitleChanged(string title)
        {
            EventHandler<TerminalTitleEventArgs> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, new TerminalTitleEventArgs(title));
            }
        }
    }
}
