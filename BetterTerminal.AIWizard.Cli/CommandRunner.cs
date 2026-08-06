using System;
using System.Diagnostics;
using System.IO;

namespace BetterTerminal.AIWizard.Cli
{
    /// <summary>
    /// Runs the assembled command in the resolved directory and hands it this console, so the agent
    /// draws its own full-screen interface exactly as it would if it had been typed at a prompt.
    ///
    /// The agents ship as command shims on the search path, so the command goes through the command
    /// interpreter, which finds them by name. Nothing here is redirected: the agent owns the screen
    /// until it exits. The child is deliberately left in this process's job - the one the terminal
    /// created for the pane - so closing the pane still takes the agent with it.
    /// </summary>
    internal static class CommandRunner
    {
        public static int Run(string command, string workingDirectory, EngineInfo engine)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.UseShellExecute = false;
            start.CreateNoWindow = false;
            start.WorkingDirectory = workingDirectory;
            start.FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

            // /d skips any AutoRun script, /s keeps the outer pair of quotes and /c runs and exits.
            start.Arguments = "/d /s /c \"" + command + "\"";

            // Claude needs Git Bash on Windows and reads its path from the environment; set it when
            // it is found and leave it untouched otherwise.
            if (engine.Engine == AiEngine.Claude)
            {
                string bash = GitBash.Locate();
                if (!string.IsNullOrEmpty(bash))
                {
                    start.EnvironmentVariables[GitBash.PathVariable] = bash;
                }
            }

            try
            {
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return -1;
                    }

                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return -1;
            }
        }
    }
}
