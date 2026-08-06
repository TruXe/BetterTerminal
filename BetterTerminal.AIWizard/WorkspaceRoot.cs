using System;
using System.IO;

namespace BetterTerminal.AIWizard
{
    /// <summary>
    /// Decides which directory the agent should run in. The launcher runs an agent at the project
    /// root rather than wherever it was started, so a run from a nested folder still sees the whole
    /// tree. The root is the outermost ancestor that holds a ".git" entry; when there is none, the
    /// starting directory is used unchanged.
    /// </summary>
    public static class WorkspaceRoot
    {
        /// <summary>An override the caller may set, matching the launcher's AI_BAT_ROOT.</summary>
        public const string OverrideVariable = "AI_BAT_ROOT";

        private const int WalkCap = 64;

        public static string Resolve(string startDirectory)
        {
            string overridden = Environment.GetEnvironmentVariable(OverrideVariable);
            if (!string.IsNullOrEmpty(overridden) && Directory.Exists(overridden))
            {
                return overridden;
            }

            string start = string.IsNullOrEmpty(startDirectory) ? Environment.CurrentDirectory : startDirectory;
            if (!Directory.Exists(start))
            {
                return start;
            }

            string outermost = null;
            DirectoryInfo current = new DirectoryInfo(start);

            for (int step = 0; step < WalkCap && current != null; step++)
            {
                if (HasGit(current.FullName))
                {
                    // Keep climbing: the outermost repository wins, so a submodule inside a larger
                    // checkout is measured against the checkout, not against itself.
                    outermost = current.FullName;
                }

                current = current.Parent;
            }

            return outermost ?? start;
        }

        private static bool HasGit(string directory)
        {
            try
            {
                string marker = Path.Combine(directory, ".git");
                return Directory.Exists(marker) || File.Exists(marker);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
