using System;
using System.IO;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Keeps a copy of the application under the local application data folder and runs the
    /// command from there. Two reasons, both real:
    ///
    /// the build output directory is wherever the developer put it - it can be deleted, moved, or
    /// sit on a drive that is not always present, and the command would then point at nothing; and
    /// that path may contain characters the command interpreter cannot read back, which is exactly
    /// how the first version of the command failed on this machine. The install folder is under the
    /// user profile, and the script reaches the executable relative to itself, so no unusual
    /// character ever has to survive a round trip through a script file.
    ///
    /// Per user, no elevation, no installer, and nothing is registered with the system beyond the
    /// search-path entry that <see cref="CommandRegistration"/> writes.
    /// </summary>
    public static class SelfInstall
    {
        public const string ExecutableName = "BetterTerminal.exe";

        public static string InstallDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BetterTerminal",
                    "app");
            }
        }

        public static string InstalledExecutable
        {
            get { return Path.Combine(InstallDirectory, ExecutableName); }
        }

        /// <summary>True when this process is already the installed copy.</summary>
        public static bool IsRunningInstalled()
        {
            string current = CurrentDirectory();
            return current != null && string.Equals(
                current.TrimEnd(Path.DirectorySeparatorChar),
                InstallDirectory.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Copies the application next to itself in the install folder on the first run, and
        /// refreshes it whenever the running build is newer. Returns the installed executable, or
        /// null when there is nothing usable there - the caller then falls back to this process.
        /// </summary>
        public static string Ensure()
        {
            if (IsRunningInstalled())
            {
                return InstalledExecutable;
            }

            string source = CurrentDirectory();
            if (source == null)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(InstallDirectory);

                foreach (string file in Directory.GetFiles(source, "BetterTerminal*"))
                {
                    string extension = Path.GetExtension(file);
                    if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".config", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Copy(file, Path.Combine(InstallDirectory, Path.GetFileName(file)));
                }
            }
            catch (IOException)
            {
                // An installed copy that is currently running cannot be replaced. That is fine:
                // the copy already there keeps working, and the next start updates it.
            }
            catch (UnauthorizedAccessException)
            {
            }

            return File.Exists(InstalledExecutable) ? InstalledExecutable : null;
        }

        private static void Copy(string source, string destination)
        {
            if (File.Exists(destination) &&
                File.GetLastWriteTimeUtc(destination) >= File.GetLastWriteTimeUtc(source))
            {
                return;
            }

            File.Copy(source, destination, true);
        }

        private static string CurrentDirectory()
        {
            string path = typeof(SelfInstall).Assembly.Location;
            return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
        }
    }
}
