using System;
using System.IO;

namespace BetterTerminal.Terminal
{
    public sealed class ShellProfile
    {
        public ShellProfile(string name, string executable, string arguments)
        {
            Name = name;
            Executable = executable;
            Arguments = arguments;
        }

        public string Name { get; private set; }

        public string Executable { get; private set; }

        public string Arguments { get; private set; }

        public static ShellProfile CommandPrompt
        {
            get
            {
                return new ShellProfile(
                    "Command Prompt",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                    string.Empty);
            }
        }

        public static ShellProfile WindowsPowerShell
        {
            get
            {
                return new ShellProfile(
                    "Windows PowerShell",
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell\\v1.0\\powershell.exe"),
                    "-NoLogo");
            }
        }

        /// <summary>
        /// The guided command builder for CLI AI agents. Like the banner program it is reached by
        /// name, from the folder the command registration puts on the search path, so no path is
        /// ever quoted into a command line. Its arguments are empty: the wizard reads the pane's
        /// working directory from the environment and needs nothing on its own command line.
        /// </summary>
        public static ShellProfile CliAiWizard
        {
            get { return new ShellProfile("CLI-AI Wizard", "beterm-aiwizard.exe", string.Empty); }
        }

        /// <summary>
        /// The same shell started differently. The name is kept, because that is what the saved
        /// layout stores and what a restored pane looks the profile up by.
        /// </summary>
        public ShellProfile WithArguments(string arguments)
        {
            return new ShellProfile(Name, Executable, arguments);
        }

        public string BuildCommandLine()
        {
            string quoted = "\"" + Executable + "\"";
            return string.IsNullOrEmpty(Arguments) ? quoted : quoted + " " + Arguments;
        }
    }
}
