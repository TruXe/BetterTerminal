using System;
using System.Collections.Generic;

namespace BetterTerminal.Wrap
{
    /// <summary>The list of scripts. Entry point of the program and the place every run returns to.</summary>
    public sealed class PickerScreen : Screen
    {
        private const int RowsPerScript = 3;

        private readonly IList<ScriptEntry> _scripts;
        private readonly string _toolsFolder;

        private int _selected;

        public PickerScreen(IList<ScriptEntry> scripts, string toolsFolder)
        {
            _scripts = scripts;
            _toolsFolder = toolsFolder;
        }

        public override string Title
        {
            get { return "Scripts"; }
        }

        public override string Context
        {
            get { return _toolsFolder; }
        }

        public override string KeyHelp
        {
            get { return "Up/Down=select  Enter=open  Q=quit"; }
        }

        public override void RenderBody(AnsiWriter writer, int top, int width, int height)
        {
            writer.Panel(top, 1, width - 2, height, Palette.StrokeDefault, Palette.Surface,
                "Scripts", Palette.TextSecondary);

            if (_scripts.Count == 0)
            {
                writer.MoveTo(top + 2, 4);
                writer.Background(Palette.Surface);
                writer.Foreground(Palette.TextSecondary);
                writer.WriteClipped("No known script in that folder.", width - 8);
                return;
            }

            int inner = width - 6;

            for (int index = 0; index < _scripts.Count; index++)
            {
                int row = top + 1 + index * RowsPerScript;
                if (row + 1 >= top + height - 1)
                {
                    break;
                }

                RenderScript(writer, row, inner, index);
            }
        }

        private void RenderScript(AnsiWriter writer, int row, int inner, int index)
        {
            ScriptEntry script = _scripts[index];
            bool current = index == _selected;
            TerminalColor background = current ? Palette.Elevated : Palette.Surface;

            writer.Fill(row, 2, inner + 2, background);
            writer.Fill(row + 1, 2, inner + 2, background);

            if (current)
            {
                writer.FocusEdge(row, 2, Palette.Accent, background);
                writer.FocusEdge(row + 1, 2, Palette.Accent, background);
            }

            writer.MoveTo(row, 4);
            writer.Background(background);
            writer.Foreground(current ? Palette.AccentLight : Palette.TextPrimary);
            writer.WriteClipped(script.FileName, inner - 20);

            if (script.TakesOverTerminal)
            {
                writer.MoveTo(row, inner - 12);
                writer.Background(Palette.Surface);
                writer.Foreground(Palette.Warning);
                writer.Write(" own console ");
            }

            writer.MoveTo(row + 1, 4);
            writer.Background(background);
            writer.Foreground(Palette.TextTertiary);
            writer.WriteClipped(script.Summary, inner - 2);
        }

        public override Screen HandleKey(ConsoleKeyInfo key)
        {
            if (_scripts.Count == 0)
            {
                return this;
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _selected = _selected == 0 ? _scripts.Count - 1 : _selected - 1;
                    return this;

                case ConsoleKey.DownArrow:
                    _selected = (_selected + 1) % _scripts.Count;
                    return this;

                case ConsoleKey.Enter:
                    return new ArgumentScreen(_scripts[_selected], _toolsFolder, this);

                default:
                    return this;
            }
        }
    }
}
