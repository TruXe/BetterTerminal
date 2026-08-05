using System;
using System.Collections.Generic;
using System.IO;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// The scripts under tools, described exactly as they declare themselves. Parameters, defaults
    /// and the terminal flag are transcribed from each script's own param block and header comment;
    /// when a script changes its parameters, this is the one place that has to follow.
    /// </summary>
    public static class ScriptCatalog
    {
        public const string FolderName = "tools";

        public static IList<ScriptEntry> Load(string toolsFolder)
        {
            List<ScriptEntry> scripts = new List<ScriptEntry>();

            foreach (ScriptEntry entry in Describe())
            {
                if (File.Exists(Path.Combine(toolsFolder, entry.FileName)))
                {
                    scripts.Add(entry);
                }
            }

            return scripts;
        }

        /// <summary>
        /// Walks up from the running executable looking for the tools folder, so the program works
        /// both from the build output and from a copy placed beside the repository.
        /// </summary>
        public static string FindToolsFolder(string startDirectory)
        {
            DirectoryInfo directory = new DirectoryInfo(startDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, FolderName);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static IEnumerable<ScriptEntry> Describe()
        {
            yield return new ScriptEntry(
                "capture-window.ps1",
                "Writes a PNG of the application window without taking focus.",
                false,
                new[]
                {
                    new ScriptParameter("Out", true, null, "Path of the PNG to write"),
                    new ScriptParameter("ProcessName", false, "BetterTerminal", "Process to capture")
                });

            yield return new ScriptEntry(
                "ui-smoke.ps1",
                "Invokes buttons through automation and reports whether the app survived. " +
                "The verdict is the RESULT line, not the exit code.",
                false,
                new[]
                {
                    new ScriptParameter("Exe", true, null, "Executable to drive"),
                    new ScriptParameter("Log", true, null, "Log file to write"),
                    new ScriptParameter("Sequence", false, "New tab|Close pane",
                        "Automation names separated by |; the default no longer matches a button"),
                    new ScriptParameter("StepDelaySeconds", false, "3", "Pause between steps")
                });

            // The next two start a shell that inherits the standard handles. Piped here, the child
            // would write into the pipe instead of into its pseudo console and every measurement
            // would come back empty - so they get the console to themselves.
            yield return new ScriptEntry(
                "flood-benchmark.ps1",
                "Measures throughput of one session end to end. Needs the console to itself.",
                true,
                new[]
                {
                    new ScriptParameter("Bin", true, null, "Build output directory holding the DLLs"),
                    new ScriptParameter("Work", true, null, "Scratch directory for the payload"),
                    new ScriptParameter("Log", true, null, "Log file to write"),
                    new ScriptParameter("Lines", false, "87000", "Payload size in lines")
                });

            yield return new ScriptEntry(
                "session-cycle.ps1",
                "Opens and closes many sessions and counts what was left behind. " +
                "Needs the console to itself.",
                true,
                new[]
                {
                    new ScriptParameter("Bin", true, null, "Build output directory holding the DLLs"),
                    new ScriptParameter("Log", true, null, "Log file to write"),
                    new ScriptParameter("Cycles", false, "20", "Number of cycles"),
                    new ScriptParameter("SessionsPerCycle", false, "4", "Sessions opened per cycle")
                });
        }
    }
}
