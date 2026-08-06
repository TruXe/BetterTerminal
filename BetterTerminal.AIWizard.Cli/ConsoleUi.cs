using System;
using BetterTerminal.Wrap;

namespace BetterTerminal.AIWizard.Cli
{
    /// <summary>
    /// The line-based drawing this front end needs, on top of the shared writer and palette so the
    /// wizard reads as the same product as the rest of the terminal. It does not take the screen
    /// over - it prints and scrolls like an ordinary prompt, which is what lets a full-screen agent
    /// draw normally the moment the wizard hands the console across.
    /// </summary>
    internal sealed class ConsoleUi
    {
        private readonly AnsiWriter _writer = new AnsiWriter();

        public void Segment(TerminalColor colour, string text)
        {
            _writer.Foreground(colour);
            _writer.Write(text);
            _writer.ResetAttributes();
            _writer.Flush();
        }

        public void Line(TerminalColor colour, string text)
        {
            Segment(colour, text + "\r\n");
        }

        public void Line(string text)
        {
            Line(Palette.TextPrimary, text);
        }

        public void Blank()
        {
            Console.Write("\r\n");
        }

        public void Rule()
        {
            Line(Palette.StrokeDefault, new string('-', 58));
        }

        public void Title(string heading, string subheading)
        {
            Console.Write("\r\n");
            Segment(Palette.Accent, heading);
            if (!string.IsNullOrEmpty(subheading))
            {
                Segment(Palette.TextTertiary, "   " + subheading);
            }

            Console.Write("\r\n");
        }

        public void Field(string label, TerminalColor valueColour, string value)
        {
            Segment(Palette.TextSecondary, label + ": ");
            Line(valueColour, value);
        }

        /// <summary>An option row: an accented key in brackets, then its label and an optional hint.</summary>
        public void Option(char key, string label, string hint)
        {
            Segment(Palette.TextTertiary, "  [");
            Segment(Palette.AccentLight, key.ToString());
            Segment(Palette.TextTertiary, "] ");
            Segment(Palette.TextPrimary, label);
            if (!string.IsNullOrEmpty(hint))
            {
                Segment(Palette.TextTertiary, "  " + hint);
            }

            Console.Write("\r\n");
        }

        public void Note(string text)
        {
            Line(Palette.TextTertiary, text);
        }

        public void Error(string text)
        {
            Line(Palette.Error, text);
        }

        /// <summary>Reads a single key without echoing it, upper-cased for matching.</summary>
        public char ReadKey()
        {
            Segment(Palette.AccentLight, "  > ");
            ConsoleKeyInfo info = Console.ReadKey(true);

            if (info.Key == ConsoleKey.Enter)
            {
                Console.Write("\r\n");
                return '\r';
            }

            char pressed = info.KeyChar;
            Segment(Palette.TextPrimary, pressed.ToString());
            Console.Write("\r\n");
            return char.ToUpperInvariant(pressed);
        }

        /// <summary>Reads a line of text, shown as the user types it.</summary>
        public string ReadLine(string prompt)
        {
            Segment(Palette.TextSecondary, "  " + prompt + ": ");
            _writer.Foreground(Palette.TextPrimary);
            _writer.Flush();
            string value = Console.ReadLine();
            _writer.ResetAttributes();
            _writer.Flush();
            return value ?? string.Empty;
        }
    }
}
