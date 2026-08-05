using System;
using System.Collections.Generic;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// One input field per declared parameter, plus the command as it will be issued. Typing goes
    /// into the focused field, so the console cursor stays hidden and the field draws its own.
    /// </summary>
    public sealed class ArgumentScreen : Screen
    {
        private const int RowsPerField = 4;

        private readonly ScriptEntry _script;
        private readonly string _toolsFolder;
        private readonly PickerScreen _picker;
        private readonly List<InputField> _fields = new List<InputField>();

        private int _selected;
        private string _refusal;

        public ArgumentScreen(ScriptEntry script, string toolsFolder, PickerScreen picker)
        {
            _script = script;
            _toolsFolder = toolsFolder;
            _picker = picker;

            foreach (ScriptParameter parameter in script.Parameters)
            {
                _fields.Add(new InputField(parameter.DefaultValue == null
                    ? "not set"
                    : "not set; script uses " + parameter.DefaultValue));
            }
        }

        public override string Title
        {
            get { return _script.FileName; }
        }

        public override string Context
        {
            get { return _script.TakesOverTerminal ? "runs on its own console" : "output captured"; }
        }

        public override string KeyHelp
        {
            get { return "Up/Down=field  Left/Right=caret  Enter=run  Esc=back"; }
        }

        public override void RenderBody(AnsiWriter writer, int top, int width, int height)
        {
            int panelHeight = Math.Max(6, height - 4);
            writer.Panel(top, 1, width - 2, panelHeight, Palette.StrokeDefault, Palette.Surface,
                "Parameters", Palette.TextSecondary);

            int fieldWidth = width - 10;

            for (int index = 0; index < _fields.Count; index++)
            {
                int row = top + 1 + index * RowsPerField;
                if (row + 2 >= top + panelHeight - 1)
                {
                    break;
                }

                RenderField(writer, row, fieldWidth, index);
            }

            RenderCommand(writer, top + panelHeight, width);
        }

        private void RenderField(AnsiWriter writer, int row, int fieldWidth, int index)
        {
            ScriptParameter parameter = _script.Parameters[index];
            bool focused = index == _selected;

            writer.MoveTo(row, 4);
            writer.Background(Palette.Surface);
            writer.Foreground(focused ? Palette.AccentLight : Palette.TextSecondary);
            writer.Write(parameter.Name);

            if (parameter.Required)
            {
                writer.Foreground(Palette.TextTertiary);
                writer.Write("  required");
            }

            _fields[index].Render(writer, row + 1, 4, fieldWidth, focused);

            writer.MoveTo(row + 2, 6);
            writer.Background(Palette.Surface);
            writer.Foreground(Palette.TextTertiary);
            writer.WriteClipped(parameter.Description, fieldWidth - 4);
        }

        /// <summary>
        /// The command line as it will really be issued. Seeing it before it runs is the point:
        /// nothing is hidden behind the interface.
        /// </summary>
        private void RenderCommand(AnsiWriter writer, int row, int width)
        {
            writer.Panel(row, 1, width - 2, 4, Palette.StrokeSubtle, Palette.Chrome,
                "Command", Palette.TextSecondary);

            RunRequest request = BuildRequest();
            string arguments = request.BuildArguments();

            writer.MoveTo(row + 1, 4);
            writer.Background(Palette.Chrome);
            writer.Foreground(Palette.TextSecondary);
            writer.WriteClipped(_script.FileName + " " + arguments, width - 8);

            writer.MoveTo(row + 2, 4);
            writer.Background(Palette.Chrome);

            if (_refusal != null)
            {
                writer.Foreground(Palette.Error);
                writer.WriteClipped(_refusal, width - 8);
                return;
            }

            if (_script.TakesOverTerminal)
            {
                writer.Foreground(Palette.Warning);
                writer.WriteClipped(
                    "This script needs the console to itself; the interface steps aside while it runs.",
                    width - 8);
                return;
            }

            writer.Foreground(Palette.TextTertiary);
            writer.WriteClipped("Output is captured and shown here.", width - 8);
        }

        public override Screen HandleKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    return _picker;

                case ConsoleKey.Enter:
                    return Run();

                case ConsoleKey.UpArrow:
                    _selected = _selected == 0 ? _fields.Count - 1 : _selected - 1;
                    return this;

                case ConsoleKey.DownArrow:
                case ConsoleKey.Tab:
                    _selected = (_selected + 1) % _fields.Count;
                    return this;

                default:
                    if (_fields[_selected].HandleKey(key))
                    {
                        _refusal = null;
                    }

                    return this;
            }
        }

        private RunRequest BuildRequest()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            for (int index = 0; index < _fields.Count; index++)
            {
                values[_script.Parameters[index].Name] = _fields[index].Value;
            }

            return new RunRequest(_script, _toolsFolder, values);
        }

        private Screen Run()
        {
            RunRequest request = BuildRequest();
            IList<string> missing = request.MissingRequired();

            if (missing.Count > 0)
            {
                _refusal = "Still needed: " + string.Join(", ", new List<string>(missing).ToArray());
                return this;
            }

            _refusal = null;
            return new OutputScreen(request, _picker);
        }
    }
}
