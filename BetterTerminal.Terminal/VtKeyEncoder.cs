using System.Text;
using System.Windows.Input;

namespace BetterTerminal.Terminal
{
    public static class VtKeyEncoder
    {
        private const int ShiftPressed = 0x0010;
        private const int LeftControlPressed = 0x0008;
        private const int LeftAltPressed = 0x0002;

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

        /// <summary>
        /// Encodes typed text as whole key events for a console host that asked for them.
        /// </summary>
        /// <remarks>
        /// A host that receives a bare character has to work out which key produced it, and it does
        /// that against its own keyboard layout - not the one this window is using. Every character
        /// that needs Shift on that layout then arrives wrapped in separate Shift key events: all of
        /// 1 to 9 on a Czech layout, and every capital letter on any layout. Programs that read key
        /// events rather than a line of text stop at the Shift event and never see the character, so
        /// the key does nothing at all. Sending the key ourselves removes the guess.
        /// </remarks>
        public static string EncodeText(string text, Key key, ModifierKeys modifiers)
        {
            // A character with no key behind it - composed, or arriving from an input method - is
            // still delivered: the host passes a key event through on its character alone.
            int virtualKey = key == Key.None ? 0 : KeyInterop.VirtualKeyFromKey(key);
            int controlState = 0;

            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                controlState |= ShiftPressed;
            }

            if ((modifiers & ModifierKeys.Control) != 0)
            {
                controlState |= LeftControlPressed;
            }

            if ((modifiers & ModifierKeys.Alt) != 0)
            {
                controlState |= LeftAltPressed;
            }

            StringBuilder encoded = new StringBuilder(text.Length * 32);
            foreach (char character in text)
            {
                AppendKeyEvent(encoded, virtualKey, character, controlState, true);
                AppendKeyEvent(encoded, virtualKey, character, controlState, false);
            }

            return encoded.ToString();
        }

        /// <summary>
        /// Encodes one key press as a whole key event for a console host that asked for them.
        /// </summary>
        /// <remarks>
        /// Escape is the reason this exists. Every other key this window sends is either a printable
        /// character or a complete sequence - <c>CSI D</c> for Left, carriage return for Enter - and a
        /// host in win32 input mode still resolves those. A lone escape byte is the one input that
        /// stays ambiguous: it is also how every sequence begins, so the host's parser holds it
        /// waiting for the rest and the key never arrives. Stating the key removes the ambiguity.
        /// </remarks>
        public static string EncodeKeyEvent(Key key, char character, ModifierKeys modifiers)
        {
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            int controlState = 0;

            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                controlState |= ShiftPressed;
            }

            if ((modifiers & ModifierKeys.Control) != 0)
            {
                controlState |= LeftControlPressed;
            }

            if ((modifiers & ModifierKeys.Alt) != 0)
            {
                controlState |= LeftAltPressed;
            }

            StringBuilder encoded = new StringBuilder(64);
            AppendKeyEvent(encoded, virtualKey, character, controlState, true);
            AppendKeyEvent(encoded, virtualKey, character, controlState, false);
            return encoded.ToString();
        }

        // Virtual key, scan code, character, key down, control key state, repeat count. The scan
        // code is left at zero: it describes the physical key, which nothing downstream reads.
        private static void AppendKeyEvent(
            StringBuilder encoded, int virtualKey, char character, int controlState, bool down)
        {
            encoded.Append("\x1b[")
                .Append(virtualKey)
                .Append(";0;")
                .Append((int)character)
                .Append(down ? ";1;" : ";0;")
                .Append(controlState)
                .Append(";1_");
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
