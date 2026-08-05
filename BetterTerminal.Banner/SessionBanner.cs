using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BetterTerminal.Wrap;

namespace BetterTerminal.Banner
{
    /// <summary>
    /// What a session says about itself when it opens: the mark, then the facts that decide what
    /// the commands typed here will act on - which folder, which project, which machine.
    ///
    /// It is a separate program on purpose. The application cannot draw this itself: the screen
    /// belongs to the shell from the moment it starts, and writing into the same grid from the
    /// outside would race the parser reading the shell's own output. Running as the shell's first
    /// command puts it in the right place in the order without any of that.
    /// </summary>
    public sealed class SessionBanner
    {
        private const int NarrowConsole = 52;
        private const int LogoLineMilliseconds = 22;
        private const int CharacterMilliseconds = 2;
        private const int SpinnerMilliseconds = 90;
        private const int LabelWidth = 12;

        // Plain ASCII: the mark has to survive a console that is not showing a Unicode font.
        private static readonly string[] Mark =
        {
            @"  ___      _   _",
            @" | _ ) ___| |_| |_ ___ _ _",
            @" | _ \/ -_)  _|  _/ -_) '_|",
            @" |___/\___|\__|\__\___|_|"
        };

        private readonly AnsiWriter _writer = new AnsiWriter();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly bool _decorated;
        private readonly int _width;

        public SessionBanner(bool decorated, int width)
        {
            _decorated = decorated;
            _width = width;
            _clock.Start();
        }

        public void Write(string shellName)
        {
            WriteMark();
            WriteFacts(shellName);

            Reset();
            _writer.Write(Environment.NewLine);
            _writer.Flush();
        }

        private void WriteMark()
        {
            _writer.Write(Environment.NewLine);

            if (_width < NarrowConsole)
            {
                // No room for the mark; the wordmark alone still says whose session this is.
                Colour(Palette.AccentLight);
                _writer.Write(" BetterTerminal" + Environment.NewLine + Environment.NewLine);
                _writer.Flush();
                return;
            }

            for (int line = 0; line < Mark.Length; line++)
            {
                Colour(line < 2 ? Palette.AccentLight : Palette.Accent);
                _writer.Write(Mark[line]);

                if (line == Mark.Length - 1)
                {
                    Colour(Palette.TextTertiary);
                    _writer.Write("   T E R M I N A L");
                }

                _writer.Write(Environment.NewLine);
                _writer.Flush();
                Pause(LogoLineMilliseconds);
            }

            _writer.Write(Environment.NewLine);
            _writer.Flush();
        }

        private void WriteFacts(string shellName)
        {
            List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>();
            Add(facts, "Workspace", Environment.CurrentDirectory);
            Add(facts, "Project", Environment.GetEnvironmentVariable("BETERM_PROJECT"));
            Add(facts, "Shell", shellName);
            Add(facts, "Machine", Environment.MachineName);

            // The two shapes alternate down the block, so the eye follows the lines being written
            // rather than watching one indicator sit in the same place.
            Spinner[] shapes = { Spinner.Circle(), Spinner.Star() };

            for (int index = 0; index < facts.Count; index++)
            {
                WriteFact(facts[index].Key, facts[index].Value, shapes[index % shapes.Length]);
            }
        }

        private static void Add(ICollection<KeyValuePair<string, string>> facts, string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                facts.Add(new KeyValuePair<string, string>(label, value));
            }
        }

        /// <summary>
        /// One fact: the spinner marks the line while it is being written and is replaced by a
        /// still bullet once it is done, so a finished line never looks like it is still working.
        /// </summary>
        private void WriteFact(string label, string value, Spinner spinner)
        {
            Spin(spinner);

            _writer.Write("\r ");
            Colour(Palette.Accent);
            _writer.Write("- ");
            Colour(Palette.TextTertiary);
            _writer.Write(label.PadRight(LabelWidth));
            Colour(Palette.TextPrimary);
            _writer.Flush();

            Type(Clip(value));

            _writer.Write(Environment.NewLine);
            _writer.Flush();
        }

        private void Spin(Spinner spinner)
        {
            if (!_decorated)
            {
                return;
            }

            long until = _clock.ElapsedMilliseconds + SpinnerMilliseconds;

            while (_clock.ElapsedMilliseconds < until)
            {
                _writer.Write("\r ");
                Colour(Palette.AccentLight);
                _writer.Write(spinner.Frame(_clock.ElapsedMilliseconds).ToString());
                _writer.Flush();
                Thread.Sleep(30);
            }
        }

        private void Type(string text)
        {
            if (!_decorated)
            {
                _writer.Write(text);
                return;
            }

            foreach (char character in text)
            {
                _writer.Write(character.ToString());
                _writer.Flush();
                Pause(CharacterMilliseconds);
            }
        }

        /// <summary>A long path is shortened from the left; its end is the part that identifies it.</summary>
        private string Clip(string value)
        {
            int room = _width - LabelWidth - 6;

            if (room <= 8 || value.Length <= room)
            {
                return value;
            }

            return "..." + value.Substring(value.Length - room + 3);
        }

        /// <summary>
        /// Colour is written only when the console will act on it. Without this the sequences
        /// themselves end up in whatever the output was redirected into.
        /// </summary>
        private void Colour(TerminalColor colour)
        {
            if (_decorated)
            {
                _writer.Foreground(colour);
            }
        }

        private void Reset()
        {
            if (_decorated)
            {
                _writer.ResetAttributes();
            }
        }

        private void Pause(int milliseconds)
        {
            if (_decorated)
            {
                Thread.Sleep(milliseconds);
            }
        }

        /// <summary>
        /// Reads the width the shell has to draw in. A redirected or unusually small console
        /// reports nothing useful, so the banner falls back to a width that always fits.
        /// </summary>
        public static int ConsoleWidth()
        {
            try
            {
                int width = Console.WindowWidth;
                return width < 20 ? NarrowConsole : width;
            }
            catch (IOException)
            {
                return NarrowConsole;
            }
        }
    }
}
