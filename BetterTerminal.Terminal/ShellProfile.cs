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
