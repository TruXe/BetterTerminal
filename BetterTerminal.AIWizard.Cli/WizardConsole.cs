using System;
using System.Collections.Generic;
using System.IO;
using BetterTerminal.Wrap;

namespace BetterTerminal.AIWizard.Cli
{
    /// <summary>
    /// Drives the wizard: choose an agent, walk its steps, review the assembled command and run it.
    /// Every step supports going back a screen and quitting, so a wrong turn costs one key rather
    /// than a restart. The flows themselves come from the shared library, ported from the ai.bat
    /// launcher by Deerpfy (github.com/Deerpfy).
    /// </summary>
    internal sealed class WizardConsole
    {
        private enum StepKind { Advance, Back, Quit }

        private struct StepResult
        {
            public StepKind Kind;
            public string Fragment;

            public static StepResult Advance(string fragment)
            {
                return new StepResult { Kind = StepKind.Advance, Fragment = fragment };
            }

            public static readonly StepResult Back = new StepResult { Kind = StepKind.Back };
            public static readonly StepResult Quit = new StepResult { Kind = StepKind.Quit };
        }

        private readonly ConsoleUi _ui = new ConsoleUi();
        private readonly ModelCatalog _models = ModelCatalog.Load();
        private readonly TerminalMode _terminal;
        private string _workingDirectory;

        public WizardConsole(string startDirectory, TerminalMode terminal)
        {
            _terminal = terminal;
            _workingDirectory = WorkspaceRoot.Resolve(startDirectory);
        }

        public int Run()
        {
            Header();

            while (true)
            {
                EngineInfo engine = SelectEngine();
                if (engine == null)
                {
                    _ui.Blank();
                    _ui.Note("Closed the wizard.");
                    return 0;
                }

                if (!RunEngine(engine))
                {
                    _ui.Blank();
                    _ui.Note("Closed the wizard.");
                    return 0;
                }
            }
        }

        private void Header()
        {
            _ui.Blank();
            _ui.Line(Palette.Accent, WizardInfo.Title + "  " + WizardInfo.Version);
            _ui.Note("Guided command builder for CLI AI agents.");
            _ui.Note("Ported from the ai.bat launcher by Deerpfy - github.com/Deerpfy");
            _ui.Rule();
        }

        private EngineInfo SelectEngine()
        {
            _ui.Title("Choose an agent", null);
            _ui.Field("Working directory", Palette.TextPrimary, _workingDirectory);
            _ui.Blank();

            IList<EngineInfo> engines = EngineInfo.All;
            for (int index = 0; index < engines.Count; index++)
            {
                _ui.Option((char)('1' + index), engines[index].DisplayName, engines[index].Command);
            }

            _ui.Option('Q', "Quit", null);

            while (true)
            {
                char key = _ui.ReadKey();
                if (key == 'Q')
                {
                    return null;
                }

                int choice = key - '1';
                if (choice >= 0 && choice < engines.Count)
                {
                    return engines[choice];
                }

                _ui.Error("Not one of the choices - try again.");
            }
        }

        /// <summary>Returns true to keep going (agent menu), false to close the wizard.</summary>
        private bool RunEngine(EngineInfo engine)
        {
            IList<WizardStep> steps = WizardCatalog.StepsFor(engine.Engine);
            string[] recorded = new string[steps.Count];

            int index = 0;
            while (index < steps.Count)
            {
                StepResult result = steps[index].IsModel
                    ? RenderModel(steps[index], engine)
                    : RenderMenu(steps[index]);

                if (result.Kind == StepKind.Quit)
                {
                    return false;
                }

                if (result.Kind == StepKind.Back)
                {
                    if (index == 0)
                    {
                        return true;
                    }

                    index--;
                    continue;
                }

                recorded[index] = result.Fragment;
                index++;
            }

            CommandComposer composer = new CommandComposer(engine);
            foreach (string fragment in recorded)
            {
                composer.Add(fragment);
            }

            return Review(composer, engine);
        }

        private StepResult RenderMenu(WizardStep step)
        {
            while (true)
            {
                _ui.Title(step.Title, null);
                if (!string.IsNullOrEmpty(step.Note))
                {
                    _ui.Note(step.Note);
                }

                foreach (WizardChoice choice in step.Choices)
                {
                    _ui.Option(choice.Key, choice.Label, choice.Hint);
                }

                _ui.Option('B', "Back", null);
                _ui.Option('Q', "Quit", null);

                char key = _ui.ReadKey();
                if (key == 'B')
                {
                    return StepResult.Back;
                }

                if (key == 'Q')
                {
                    return StepResult.Quit;
                }

                WizardChoice picked = Find(step.Choices, key);
                if (picked == null)
                {
                    _ui.Error("Not one of the choices - try again.");
                    continue;
                }

                if (!picked.PromptValue)
                {
                    return StepResult.Advance(picked.Fragment);
                }

                string value = _ui.ReadLine(picked.ValuePrompt);
                return StepResult.Advance(CommandComposer.FillValue(picked.Fragment, value));
            }
        }

        private StepResult RenderModel(WizardStep step, EngineInfo engine)
        {
            IList<ModelEntry> entries = _models.For(step.Engine);
            int shown = Math.Min(entries.Count, 9);

            while (true)
            {
                _ui.Title(step.Title, engine.DisplayName);
                for (int index = 0; index < shown; index++)
                {
                    _ui.Option((char)('1' + index), entries[index].Id, entries[index].Description);
                }

                _ui.Option('C', "Custom id", null);
                _ui.Option('K', "Skip (agent default)", null);
                _ui.Option('B', "Back", null);
                _ui.Option('Q', "Quit", null);

                char key = _ui.ReadKey();
                if (key == 'B')
                {
                    return StepResult.Back;
                }

                if (key == 'Q')
                {
                    return StepResult.Quit;
                }

                if (key == 'K')
                {
                    return StepResult.Advance(string.Empty);
                }

                if (key == 'C')
                {
                    string custom = TextSanitizer.Clean(_ui.ReadLine("Model id"));
                    return StepResult.Advance(custom.Length == 0
                        ? string.Empty
                        : step.ModelFlag + " " + TextSanitizer.Quote(custom));
                }

                int choice = key - '1';
                if (choice >= 0 && choice < shown)
                {
                    return StepResult.Advance(step.ModelFlag + " " + entries[choice].Id);
                }

                _ui.Error("Not one of the choices - try again.");
            }
        }

        /// <summary>Returns true to keep going, false to close the wizard.</summary>
        private bool Review(CommandComposer composer, EngineInfo engine)
        {
            while (true)
            {
                string command = composer.Build();

                _ui.Title("Review", null);
                _ui.Field("Directory", Palette.TextPrimary, _workingDirectory);
                _ui.Segment(Palette.TextSecondary, "Command: ");
                _ui.Line(Palette.AccentLight, command);
                _ui.Blank();
                _ui.Option('R', "Run", null);
                _ui.Option('D', "Change directory", null);
                _ui.Option('E', "Back to the agent menu", null);
                _ui.Option('Q', "Quit", null);

                char key = _ui.ReadKey();
                if (key == 'Q')
                {
                    return false;
                }

                if (key == 'E')
                {
                    return true;
                }

                if (key == 'D')
                {
                    ChangeDirectory();
                    continue;
                }

                if (key == 'R' || key == '\r')
                {
                    return Launch(command, engine);
                }

                _ui.Error("Not one of the choices - try again.");
            }
        }

        private void ChangeDirectory()
        {
            string entered = TextSanitizer.Clean(_ui.ReadLine("Directory path"));
            if (entered.Length == 0)
            {
                return;
            }

            if (Directory.Exists(entered))
            {
                _workingDirectory = entered;
            }
            else
            {
                _ui.Error("No such directory - keeping the current one.");
            }
        }

        private bool Launch(string command, EngineInfo engine)
        {
            _ui.Blank();
            _ui.Line(Palette.Success, "Starting " + engine.DisplayName + "...");
            _ui.Rule();

            // Hand the console back to the agent as an ordinary one before it starts, and take it
            // back only once it has exited. Without this the agent would inherit the wizard's own
            // console mode and could stall waiting on a screen it does not own.
            int exitCode;
            if (_terminal != null)
            {
                _terminal.Suspend();
                try
                {
                    exitCode = CommandRunner.Run(command, _workingDirectory, engine);
                }
                finally
                {
                    _terminal.Resume();
                }
            }
            else
            {
                exitCode = CommandRunner.Run(command, _workingDirectory, engine);
            }

            _ui.Rule();
            _ui.Field(engine.DisplayName + " exit code",
                exitCode == 0 ? Palette.Success : Palette.Warning,
                exitCode.ToString());
            _ui.Blank();
            _ui.Option('N', "Build another command", null);
            _ui.Option('Q', "Quit", null);

            while (true)
            {
                char key = _ui.ReadKey();
                if (key == 'Q')
                {
                    return false;
                }

                if (key == 'N' || key == '\r')
                {
                    return true;
                }

                _ui.Error("Not one of the choices - try again.");
            }
        }

        private static WizardChoice Find(IEnumerable<WizardChoice> choices, char key)
        {
            foreach (WizardChoice choice in choices)
            {
                if (choice.Key == key)
                {
                    return choice;
                }
            }

            return null;
        }
    }
}
