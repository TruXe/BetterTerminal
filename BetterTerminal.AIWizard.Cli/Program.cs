using System;
using BetterTerminal.Wrap;

namespace BetterTerminal.AIWizard.Cli
{
    /// <summary>
    /// The console front end the terminal starts in a pane when the CLI-AI Wizard profile is
    /// chosen. It turns on escape-sequence processing and UTF-8 for as long as it runs, then walks
    /// the user through building and launching an agent command.
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            using (TerminalMode mode = TerminalMode.Acquire())
            {
                try
                {
                    return new WizardConsole(Environment.CurrentDirectory, mode).Run();
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error.Message);
                    return 70;
                }
            }
        }
    }
}
