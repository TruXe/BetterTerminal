using System;
using System.Text;
using BetterTerminal.Interop;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// Owns the console state this program changes: escape-sequence processing, the encodings and
    /// the alternate screen. Every change is undone by Dispose, including on an unhandled failure,
    /// so the shell that launched this is never left in the alternate buffer or without a cursor.
    /// </summary>
    public sealed class TerminalMode : IDisposable
    {
        private readonly IntPtr _output;
        private readonly int _originalMode;
        private readonly Encoding _originalOutputEncoding;
        private readonly Encoding _originalInputEncoding;

        private bool _alternateScreen;
        private bool _restored;

        private TerminalMode(IntPtr output, int originalMode)
        {
            _output = output;
            _originalMode = originalMode;
            _originalOutputEncoding = Console.OutputEncoding;
            _originalInputEncoding = Console.InputEncoding;
        }

        /// <summary>
        /// Turns on escape-sequence processing, which the classic console host leaves off, and
        /// switches both directions to UTF-8 - without it box drawing and diacritics arrive as
        /// question marks. Returns null when there is no console to configure.
        /// </summary>
        public static TerminalMode Acquire()
        {
            IntPtr output = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
            int mode;
            if (output == IntPtr.Zero || !NativeMethods.GetConsoleMode(output, out mode))
            {
                return null;
            }

            TerminalMode terminal = new TerminalMode(output, mode);

            if ((mode & NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING) == 0 &&
                !NativeMethods.SetConsoleMode(output, mode | NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING))
            {
                return null;
            }

            UTF8Encoding utf8 = new UTF8Encoding(false);
            Console.OutputEncoding = utf8;
            Console.InputEncoding = utf8;
            return terminal;
        }

        /// <summary>
        /// Hands the console back exactly as it was before this program configured it, so a child
        /// that draws its own full-screen interface starts from a clean, ordinary console. This is
        /// the "get out of the way" step before running another program on the same console; take
        /// the console back afterwards with <see cref="Resume"/>.
        /// </summary>
        public void Suspend()
        {
            NativeMethods.SetConsoleMode(_output, _originalMode);
            Console.OutputEncoding = _originalOutputEncoding;
            Console.InputEncoding = _originalInputEncoding;
        }

        /// <summary>Turns escape-sequence processing and UTF-8 back on after a <see cref="Suspend"/>.</summary>
        public void Resume()
        {
            int mode;
            if (NativeMethods.GetConsoleMode(_output, out mode))
            {
                NativeMethods.SetConsoleMode(_output,
                    mode | NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
            }

            UTF8Encoding utf8 = new UTF8Encoding(false);
            Console.OutputEncoding = utf8;
            Console.InputEncoding = utf8;
        }

        public void EnterAlternateScreen(AnsiWriter writer)
        {
            if (_alternateScreen)
            {
                return;
            }

            writer.EnterAlternateScreen();
            writer.HideCursor();
            writer.Flush();
            _alternateScreen = true;
        }

        /// <summary>
        /// Hands the console back to a child that draws on it itself. The child inherits a normal
        /// screen with a visible cursor and the mode it would have had without this program.
        /// </summary>
        public void LeaveAlternateScreen(AnsiWriter writer)
        {
            if (!_alternateScreen)
            {
                return;
            }

            writer.ShowCursor();
            writer.LeaveAlternateScreen();
            writer.Flush();
            _alternateScreen = false;
        }

        public void Dispose()
        {
            if (_restored)
            {
                return;
            }

            _restored = true;

            AnsiWriter writer = new AnsiWriter();
            writer.ShowCursor();
            writer.ResetAttributes();
            if (_alternateScreen)
            {
                writer.LeaveAlternateScreen();
                _alternateScreen = false;
            }

            writer.Flush();

            NativeMethods.SetConsoleMode(_output, _originalMode);
            Console.OutputEncoding = _originalOutputEncoding;
            Console.InputEncoding = _originalInputEncoding;
        }
    }
}
