using System;
using System.Collections.Generic;
using System.Threading;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// The loop: redraw when something changed, read a key when one is waiting, and hand the
    /// console over to a script that needs it. Nothing here waits on child output - the readers
    /// run on their own threads and only set a flag this loop notices.
    /// </summary>
    public sealed class WrapApplication
    {
        private const int IdleMilliseconds = 30;

        private readonly TerminalMode _terminal;
        private readonly AnsiWriter _writer = new AnsiWriter();

        private Screen _screen;
        private int _width;
        private int _height;
        private bool _running = true;

        public WrapApplication(TerminalMode terminal, IList<ScriptEntry> scripts, string toolsFolder)
        {
            _terminal = terminal;
            _screen = new PickerScreen(scripts, toolsFolder);
        }

        public void Run()
        {
            // The interrupt reaches the child through the console on its own; this only keeps the
            // runtime from ending this process before the child has been dealt with.
            Console.CancelKeyPress += OnCancelKeyPress;

            _terminal.EnterAlternateScreen(_writer);

            bool dirty = true;

            while (_running)
            {
                OutputScreen output = _screen as OutputScreen;
                if (output != null && output.NeedsConsole)
                {
                    HandOverConsole(output);
                    dirty = true;
                }

                if (SizeChanged())
                {
                    dirty = true;
                }

                if (_screen.Poll())
                {
                    dirty = true;
                }

                if (dirty)
                {
                    _screen.Render(_writer, _width, _height);
                    dirty = false;
                }

                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(IdleMilliseconds);
                    continue;
                }

                ConsoleKeyInfo key = Console.ReadKey(true);
                if (IsQuit(key))
                {
                    _running = false;
                    continue;
                }

                Screen next = _screen.HandleKey(key);
                if (!ReferenceEquals(next, _screen))
                {
                    _screen = next;
                }

                dirty = true;
            }

            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        /// <summary>
        /// Puts the interface away, lets the script own the console for as long as it runs, then
        /// takes the screen back. Anything the script drew stays in the normal buffer, which is
        /// where the user expects to find it afterwards.
        /// </summary>
        private void HandOverConsole(OutputScreen output)
        {
            _terminal.LeaveAlternateScreen(_writer);

            try
            {
                output.RunOnConsole();
            }
            finally
            {
                _terminal.EnterAlternateScreen(_writer);
            }
        }

        private bool SizeChanged()
        {
            int width = Math.Max(20, Console.WindowWidth);
            int height = Math.Max(6, Console.WindowHeight);

            if (width == _width && height == _height)
            {
                return false;
            }

            _width = width;
            _height = height;
            return true;
        }

        private bool IsQuit(ConsoleKeyInfo key)
        {
            if (key.Key != ConsoleKey.Q)
            {
                return false;
            }

            // Q is a character everywhere except the picker, where it is the way out.
            return _screen is PickerScreen;
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            OutputScreen output = _screen as OutputScreen;
            if (output != null)
            {
                output.Cancel();
                return;
            }

            _running = false;
        }
    }
}
