using System;
using System.Collections.Generic;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// What the run returned: the command as it was issued, the exit code the process reported,
    /// and the tail of what it wrote. The exit code is the child's own - nothing here derives,
    /// guesses or overrides it.
    /// </summary>
    public sealed class ResultScreen : Screen
    {
        private const int LabelWidth = 12;

        private readonly RunRequest _request;
        private readonly int _exitCode;
        private readonly OutputLog _log;
        private readonly PickerScreen _picker;

        public ResultScreen(RunRequest request, int exitCode, OutputLog log, PickerScreen picker)
        {
            _request = request;
            _exitCode = exitCode;
            _log = log;
            _picker = picker;
        }

        public override string Title
        {
            get { return _request.Script.FileName; }
        }

        public override string Context
        {
            get { return _exitCode == 0 ? "finished" : "failed"; }
        }

        public override string KeyHelp
        {
            get { return "R=run again  Enter=scripts  Esc=scripts"; }
        }

        public override void RenderBody(AnsiWriter writer, int top, int width, int height)
        {
            writer.Panel(top, 1, width - 2, 7, Palette.StrokeDefault, Palette.Surface,
                "Result", Palette.TextSecondary);

            writer.Background(Palette.Surface);
            WriteField(writer, top + 1, 4, LabelWidth, "Script", _request.ScriptPath,
                width - 2, Palette.TextPrimary);

            string arguments = _request.BuildArguments();
            WriteField(writer, top + 2, 4, LabelWidth, "Arguments",
                arguments.Length == 0 ? "none" : arguments, width - 2, Palette.TextSecondary);

            // The exit code is the one thing this screen exists for, so it gets a filled chip.
            writer.MoveTo(top + 4, 4);
            writer.Background(Palette.Surface);
            writer.Foreground(Palette.TextTertiary);
            writer.Write("Exit code");

            writer.MoveTo(top + 4, 4 + LabelWidth);
            writer.Background(_exitCode == 0 ? Palette.Success : Palette.Error);
            writer.Foreground(Palette.AccentInk);
            writer.Write(" " + _exitCode + " ");

            writer.Background(Palette.Surface);
            writer.Foreground(Palette.TextTertiary);
            writer.Write(_exitCode == 0
                ? "  the script reported success"
                : "  the script reported a failure");

            int tailTop = top + 8;
            int tailHeight = Math.Max(3, height - 9);
            writer.Panel(tailTop, 1, width - 2, tailHeight, Palette.StrokeSubtle, Palette.Window,
                "Last output", Palette.TextSecondary);

            int visible = tailHeight - 2;
            int first = Math.Max(0, _log.Count - visible);
            IList<string> lines = _log.Window(first, visible);

            for (int index = 0; index < lines.Count; index++)
            {
                writer.MoveTo(tailTop + 1 + index, 3);
                writer.Background(Palette.Window);
                writer.Foreground(Palette.TextSecondary);
                writer.WriteClipped(lines[index], width - 7);
            }
        }

        public override Screen HandleKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.R)
            {
                return new OutputScreen(_request, _picker);
            }

            if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Escape)
            {
                return _picker;
            }

            return this;
        }
    }
}
