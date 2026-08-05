using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// The live output of one run. A script that takes the console over is not shown here at all:
    /// it draws on the real console itself, and this screen only reports what it returned.
    /// </summary>
    public sealed class OutputScreen : Screen
    {
        private readonly Spinner _spinner = Spinner.Circle();
        private readonly RunRequest _request;
        private readonly PickerScreen _picker;
        private readonly OutputLog _log = new OutputLog();
        private readonly ChildProcess _child;
        private readonly Stopwatch _elapsed = new Stopwatch();

        private int _renderedVersion = -1;
        private long _renderedTick;
        private int _scrollOffset;
        private bool _followTail = true;
        private bool _finished;
        private bool _cancelled;
        private int _exitCode = -1;
        private int _bodyHeight = 1;

        public OutputScreen(RunRequest request, PickerScreen picker)
        {
            _request = request;
            _picker = picker;
            _child = new ChildProcess(request, WorkingDirectory(request));
            _elapsed.Start();

            if (!request.Script.TakesOverTerminal)
            {
                _child.Start(_log);
            }
        }

        /// <summary>
        /// True until a script that needs the console has had it. The application answers this by
        /// putting its own screen away, calling RunOnConsole and restoring the screen afterwards.
        /// </summary>
        public bool NeedsConsole
        {
            get { return _request.Script.TakesOverTerminal && !_finished; }
        }

        public override string Title
        {
            get { return _request.Script.FileName; }
        }

        public override string Context
        {
            get
            {
                string seconds = (_elapsed.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s";
                return _finished ? "exit " + _exitCode + "   " + seconds : seconds;
            }
        }

        public override string KeyHelp
        {
            get
            {
                if (_finished)
                {
                    return "Enter=result  Esc=scripts  PageUp/PageDown=scroll";
                }

                return "PageUp/PageDown=scroll  End=follow  Ctrl+C=stop";
            }
        }

        /// <summary>Blocks until the child is done. Called only with the interface put away.</summary>
        public void RunOnConsole()
        {
            Console.Out.Write("Running " + _request.Script.FileName + Environment.NewLine);
            Console.Out.Flush();

            _exitCode = _child.RunOnConsole();
            _elapsed.Stop();
            _log.Append("Ran on the console; nothing was captured here.");
            _finished = true;
            _renderedVersion = -1;
        }

        public override bool Poll()
        {
            if (_finished)
            {
                return false;
            }

            bool changed = _log.Version != _renderedVersion;

            // The spinner and the clock are part of the frame, so a run with no output still has
            // something to redraw about four times a second.
            if (_elapsed.ElapsedMilliseconds / 250 != _renderedTick)
            {
                changed = true;
            }

            if (_child.HasExited)
            {
                // The parameterless wait is what flushes the last lines out of the two pipes.
                _child.WaitForOutput();
                _exitCode = _child.ExitCode;
                _elapsed.Stop();
                _finished = true;
                changed = true;
            }

            return changed;
        }

        public void Cancel()
        {
            if (_finished || _cancelled)
            {
                return;
            }

            _cancelled = true;
            _log.Append("Stop requested.");
            _child.Cancel();
        }

        public override void RenderBody(AnsiWriter writer, int top, int width, int height)
        {
            _renderedVersion = _log.Version;
            _renderedTick = _elapsed.ElapsedMilliseconds / 250;
            _bodyHeight = Math.Max(1, height - 2);

            writer.Panel(top, 1, width - 2, height, Palette.StrokeDefault, Palette.Window,
                StatusTitle(), _finished ? ExitColour() : Palette.AccentLight);

            int lineCount = _log.Count;
            int first = _followTail ? Math.Max(0, lineCount - _bodyHeight) : _scrollOffset;
            IList<string> lines = _log.Window(first, _bodyHeight);
            int textWidth = width - 7;

            for (int index = 0; index < lines.Count; index++)
            {
                writer.MoveTo(top + 1 + index, 3);
                writer.Background(Palette.Window);
                writer.Foreground(Palette.TextPrimary);
                writer.WriteClipped(lines[index], textWidth);
            }

            writer.ScrollBar(top + 1, width - 3, _bodyHeight, first, _bodyHeight, lineCount,
                Palette.StrokeSubtle, Palette.Accent, Palette.Window);

            if (!_followTail && lineCount > _bodyHeight)
            {
                writer.MoveTo(top + height - 1, width - 22);
                writer.Background(Palette.Window);
                writer.Foreground(Palette.TextTertiary);
                writer.Write(" scrolled back ");
            }
        }

        private string StatusTitle()
        {
            if (_finished)
            {
                return _cancelled ? "stopped, exit " + _exitCode : "exit " + _exitCode;
            }

            return _spinner.Frame(_elapsed.ElapsedMilliseconds) + " running";
        }

        private TerminalColor ExitColour()
        {
            return _exitCode == 0 ? Palette.Success : Palette.Error;
        }

        public override Screen HandleKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.PageUp:
                    _followTail = false;
                    _scrollOffset = Math.Max(0, ScrollBase() - _bodyHeight);
                    return this;

                case ConsoleKey.PageDown:
                    _scrollOffset = Math.Min(Math.Max(0, _log.Count - _bodyHeight),
                        ScrollBase() + _bodyHeight);
                    _followTail = _scrollOffset >= _log.Count - _bodyHeight;
                    return this;

                case ConsoleKey.Home:
                    _followTail = false;
                    _scrollOffset = 0;
                    return this;

                case ConsoleKey.End:
                    _followTail = true;
                    return this;

                case ConsoleKey.Escape:
                    return _finished ? Leave(_picker) : (Screen)this;

                case ConsoleKey.Enter:
                    return _finished
                        ? Leave(new ResultScreen(_request, _exitCode, _log, _picker))
                        : (Screen)this;

                default:
                    return this;
            }
        }

        private Screen Leave(Screen next)
        {
            _child.Dispose();
            return next;
        }

        private int ScrollBase()
        {
            return _followTail ? Math.Max(0, _log.Count - _bodyHeight) : _scrollOffset;
        }

        /// <summary>
        /// Scripts are written to be run from the repository root - the folder holding tools - and
        /// their examples use paths relative to it.
        /// </summary>
        private static string WorkingDirectory(RunRequest request)
        {
            return System.IO.Directory.GetParent(
                System.IO.Path.GetDirectoryName(request.ScriptPath)).FullName;
        }
    }
}
