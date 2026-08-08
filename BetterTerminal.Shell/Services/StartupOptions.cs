using System;
using System.IO;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// What the application was asked to open. The command shim writes the directory it was
    /// invoked from onto the command line, which is the whole mechanism behind "open the folder
    /// I am standing in": nothing else in the shell reads the environment.
    /// </summary>
    public sealed class StartupOptions
    {
        public const string ProjectSwitch = "--project";

        /// <summary>
        /// Set by the service when it starts the application only to raise a notification: the
        /// application loads the notification library with the rest of the command line and shows
        /// nothing else. The notification's own text and buttons follow on the same line.
        /// </summary>
        public const string NotifySwitch = "--notify";

        private static readonly StartupOptions Instance = new StartupOptions();

        private StartupOptions()
        {
        }

        public static StartupOptions Current
        {
            get { return Instance; }
        }

        /// <summary>The folder the shell was launched from, or null for a plain launch.</summary>
        public string ProjectDirectory { get; private set; }

        public bool HasProject
        {
            get { return !string.IsNullOrEmpty(ProjectDirectory); }
        }

        /// <summary>True when the service started this application only to raise a notification.</summary>
        public bool HasNotify { get; private set; }

        /// <summary>
        /// Accepts "--project &lt;path&gt;" and a bare path, so the shim, a shortcut and a
        /// drag-and-drop onto the executable all behave the same.
        /// </summary>
        public void Parse(string[] args)
        {
            if (args == null)
            {
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (string.IsNullOrEmpty(argument))
                {
                    continue;
                }

                if (string.Equals(argument, ProjectSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        ProjectDirectory = Normalize(args[i + 1]);
                        i++;
                    }

                    continue;
                }

                if (string.Equals(argument, NotifySwitch, StringComparison.OrdinalIgnoreCase))
                {
                    // A bare flag: the notification's title, message and buttons are read from the
                    // rest of the line by the notification library, not here.
                    HasNotify = true;
                    continue;
                }

                if (ProjectDirectory == null && !argument.StartsWith("-", StringComparison.Ordinal))
                {
                    ProjectDirectory = Normalize(argument);
                }
            }
        }

        private static string Normalize(string path)
        {
            try
            {
                string full = Path.GetFullPath(path.Trim('"'));

                // A trailing separator survives "%CD%" for a drive root and breaks nothing, but
                // every display of the path is nicer without it.
                if (full.Length > 3)
                {
                    full = full.TrimEnd(Path.DirectorySeparatorChar);
                }

                return Directory.Exists(full) ? full : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }
    }
}
