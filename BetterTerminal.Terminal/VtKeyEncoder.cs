using System.Windows.Input;

namespace BetterTerminal.Terminal
{
    public static class VtKeyEncoder
    {
        // Returns null when the key carries no control sequence and should arrive as text input.
        public static string Encode(Key key, ModifierKeys modifiers, bool applicationCursorKeys)
        {
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            if (control && !alt)
            {
                string controlCode = EncodeControl(key, shift);
                if (controlCode != null)
                {
                    return controlCode;
                }
            }

            string sequence = EncodeSpecial(key, modifiers, applicationCursorKeys);
            if (sequence == null)
            {
                return null;
            }

            return alt ? "\x1b" + sequence : sequence;
        }

        private static string EncodeControl(Key key, bool shift)
        {
            if (key >= Key.A && key <= Key.Z)
            {
                // Ctrl+Shift+letter belongs to the shell: clipboard, panes, tabs, command palette.
                return shift ? null : ((char)(key - Key.A + 1)).ToString();
            }

            switch (key)
            {
                case Key.Space:
                    return "\0";
                case Key.OemOpenBrackets:
                    return "\x1b";
                case Key.OemCloseBrackets:
                    return "\x1d";
                case Key.OemBackslash:
                    return "\x1c";
                default:
                    return null;
            }
        }

        private static string EncodeSpecial(Key key, ModifierKeys modifiers, bool applicationCursorKeys)
        {
            switch (key)
            {
                case Key.Enter:
                    return "\r";
                case Key.Back:
                    return "\x7f";
                case Key.Tab:
                    return (modifiers & ModifierKeys.Shift) != 0 ? "\x1b[Z" : "\t";
                case Key.Escape:
                    return "\x1b";
                case Key.Up:
                    return CursorKey('A', modifiers, applicationCursorKeys);
                case Key.Down:
                    return CursorKey('B', modifiers, applicationCursorKeys);
                case Key.Right:
                    return CursorKey('C', modifiers, applicationCursorKeys);
                case Key.Left:
                    return CursorKey('D', modifiers, applicationCursorKeys);
                case Key.Home:
                    return CursorKey('H', modifiers, applicationCursorKeys);
                case Key.End:
                    return CursorKey('F', modifiers, applicationCursorKeys);
                case Key.Insert:
                    return "\x1b[2~";
                case Key.Delete:
                    return "\x1b[3~";
                case Key.PageUp:
                    return "\x1b[5~";
                case Key.PageDown:
                    return "\x1b[6~";
                case Key.F1:
                    return "\x1bOP";
                case Key.F2:
                    return "\x1bOQ";
                case Key.F3:
                    return "\x1bOR";
                case Key.F4:
                    return "\x1bOS";
                case Key.F5:
                    return "\x1b[15~";
                case Key.F6:
                    return "\x1b[17~";
                case Key.F7:
                    return "\x1b[18~";
                case Key.F8:
                    return "\x1b[19~";
                case Key.F9:
                    return "\x1b[20~";
                case Key.F10:
                    return "\x1b[21~";
                case Key.F11:
                    return "\x1b[23~";
                case Key.F12:
                    return "\x1b[24~";
                default:
                    return null;
            }
        }

        private static string CursorKey(char final, ModifierKeys modifiers, bool applicationCursorKeys)
        {
            int modifier = 1;
            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                modifier += 1;
            }

            if ((modifiers & ModifierKeys.Alt) != 0)
            {
                modifier += 2;
            }

            if ((modifiers & ModifierKeys.Control) != 0)
            {
                modifier += 4;
            }

            if (modifier > 1)
            {
                return "\x1b[1;" + modifier + final;
            }

            return applicationCursorKeys ? "\x1bO" + final : "\x1b[" + final;
        }
    }
}
