using System;
using System.IO;

namespace BetterTerminal.AIWizard
{
    /// <summary>
    /// Finds the Git Bash executable, which Claude needs on Windows and expects in
    /// CLAUDE_CODE_GIT_BASH_PATH. Only the standard install locations are checked; nothing is run
    /// to search, so the lookup has no side effect of its own beyond reading the file system.
    /// </summary>
    public static class GitBash
    {
        public const string PathVariable = "CLAUDE_CODE_GIT_BASH_PATH";

        public static string Locate()
        {
            foreach (string candidate in Candidates())
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string[] Candidates()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            return new[]
            {
                Path.Combine(localAppData, "Programs", "Git", "bin", "bash.exe"),
                Path.Combine(programFiles, "Git", "bin", "bash.exe"),
                Path.Combine(programFilesX86, "Git", "bin", "bash.exe"),
                Path.Combine(userProfile, "scoop", "apps", "git", "current", "bin", "bash.exe")
            };
        }
    }
}
