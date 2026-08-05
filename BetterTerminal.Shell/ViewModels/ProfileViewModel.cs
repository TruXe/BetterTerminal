using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.ViewModels
{
    public class ProfileViewModel
    {
        public string Name { get; set; }

        public string CommandLine { get; set; }

        public string StartingDirectory { get; set; }

        public string Source { get; set; }

        public string Accelerator { get; set; }

        public bool RunAsAdministrator { get; set; }

        public int ScrollbackLines { get; set; }

        /// <summary>What a new session started from this profile actually launches.</summary>
        public ShellProfile Shell { get; set; }
    }
}
