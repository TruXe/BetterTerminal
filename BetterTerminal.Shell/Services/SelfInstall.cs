using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        /// <summary>The version of the running build - the number in VersionInfo.cs.</summary>
        public static Version RunningVersion
        {
            get { return typeof(SelfInstall).Assembly.GetName().Version; }
        }

        /// <summary>
        /// The version of the copy under the user profile, or null when there is none. It is read
        /// from the file rather than loaded, so an installed copy that is currently running - or
        /// one built against a different framework - is still readable.
        /// </summary>
        public static Version InstalledVersion
        {
            get
            {
                try
                {
                    if (!File.Exists(InstalledExecutable))
                    {
                        return null;
                    }

                    string version = FileVersionInfo.GetVersionInfo(InstalledExecutable).FileVersion;
                    Version parsed;
                    return Version.TryParse(version, out parsed) ? parsed : null;
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Copies the application next to itself in the install folder on the first run, and
        /// replaces it whenever this build carries a higher version. Returns the installed
        /// executable, or null when there is nothing usable there - the caller then falls back to
        /// this process.
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

                // A higher version replaces the whole folder, file by file. At the same version the
                // per-file timestamp still decides, so a rebuilt developer build lands as well
                // without having to bump the number for every change.
                Version installed = InstalledVersion;

                // An older build never touches a newer installed copy. Without this the timestamps
                // decide, and an older application unpacked into a fresh temporary folder carries
                // newer files by definition - which is how a one-file launcher left behind by a
                // previous release quietly reinstalled the version it was carrying.
                if (installed != null && RunningVersion < installed)
                {
                    return InstalledExecutable;
                }

                bool newer = installed == null || RunningVersion > installed;

                foreach (string file in Files(source))
                {
                    Copy(file, Path.Combine(InstallDirectory, Path.GetFileName(file)), newer);
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

        /// <summary>
        /// What the installed copy consists of: the application and its libraries, and the helper
        /// programs a session starts by name. Leaving the helpers out is how the installed copy
        /// used to end up without the banner and the wizard.
        /// </summary>
        private static IEnumerable<string> Files(string source)
        {
            List<string> files = new List<string>();
            foreach (string pattern in new[] { "BetterTerminal*", "beterm-*" })
            {
                foreach (string file in Directory.GetFiles(source, pattern))
                {
                    string extension = Path.GetExtension(file);
                    if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".config", StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        private static void Copy(string source, string destination, bool force)
        {
            if (!force && File.Exists(destination) &&
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
