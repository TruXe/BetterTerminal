using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Makes the application reachable as a command from an ordinary shell prompt. A tiny script
    /// under the local application data folder starts the executable and hands it the directory
    /// it was invoked from; that folder is added to the per-user search path once.
    ///
    /// Everything here is best effort and per user: the application never elevates, so it never
    /// writes to the machine-wide search path and never touches another user's profile.
    /// </summary>
    public static class CommandRegistration
    {
        public const string CommandName = "beterm";

        private const string BannerName = "beterm-banner.exe";
        private const string HomeVariable = "BETERM_HOME";
        private const string EnvironmentKey = "Environment";
        private const string PathValue = "Path";

        public static string BinDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BetterTerminal",
                    "bin");
            }
        }

        public static string ScriptPath
        {
            get { return Path.Combine(BinDirectory, CommandName + ".cmd"); }
        }

        /// <summary>
        /// Called on every start. Rewrites the script when the executable moved, and joins the
        /// search path only when it is not already there, so a normal launch does nothing.
        /// </summary>
        public static void Ensure()
        {
            try
            {
                // The command must not depend on the folder this process happens to be running
                // from, so the application installs a copy under the user profile first and the
                // command opens that one.
                string installed = SelfInstall.Ensure();

                Directory.CreateDirectory(BinDirectory);
                WriteScript(installed ?? ExecutablePath());
                CopyBanner();
                JoinSearchPath();
            }
            catch (IOException)
            {
                // Registration is a convenience: the window still opens without it.
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        private static string ExecutablePath()
        {
            string path = typeof(CommandRegistration).Assembly.Location;
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// The banner program goes in the same folder as the command, which is the one folder this
        /// application puts on the search path - that is how a shell finds it by name alone, with
        /// no path to quote into its command line.
        /// </summary>
        private static void CopyBanner()
        {
            string executable = ExecutablePath();
            if (executable == null)
            {
                return;
            }

            string folder = Path.GetDirectoryName(executable);

            // The banner needs the interop assembly beside it, exactly as it does in the build
            // output; an executable copied on its own would fail to load on its first call.
            foreach (string name in new[] { BannerName, "BetterTerminal.Interop.dll" })
            {
                CopyIfNewer(Path.Combine(folder, name), Path.Combine(BinDirectory, name));
            }
        }

        private static void CopyIfNewer(string source, string destination)
        {
            if (!File.Exists(source))
            {
                return;
            }

            if (File.Exists(destination) &&
                File.GetLastWriteTimeUtc(destination) >= File.GetLastWriteTimeUtc(source))
            {
                return;
            }

            File.Copy(source, destination, true);
        }

        private static void WriteScript(string executable)
        {
            if (string.IsNullOrEmpty(executable))
            {
                return;
            }

            // The installed copy sits one level up and across from this script, so the script
            // reaches it through %~dp0 and contains no absolute path at all. That is what keeps
            // the file pure ASCII whatever the user profile or the build folder is called - a
            // path written into a script has to survive being read back in whatever code page
            // the console happens to be using, and an accented character does not.
            string target = string.Equals(executable, SelfInstall.InstalledExecutable,
                StringComparison.OrdinalIgnoreCase)
                ? "%~dp0..\\app\\" + SelfInstall.ExecutableName
                : executable;

            string script = string.Concat(
                "@echo off\r\n",
                "rem Generated by BetterTerminal. Opens the application with the current\r\n",
                "rem directory as its project folder. Delete this file to remove the command.\r\n",
                "start \"\" \"", target, "\" ", StartupOptions.ProjectSwitch, " \"%CD%\" %*\r\n");

            Encoding encoding = ScriptEncoding();

            if (File.Exists(ScriptPath) && File.ReadAllText(ScriptPath, encoding) == script)
            {
                return;
            }

            File.WriteAllText(ScriptPath, script, encoding);
        }

        /// <summary>
        /// The command interpreter reads a script with the console code page, which is the OEM
        /// one - not the ANSI one the framework calls "default". Writing the script as ANSI put
        /// a mangled character in the path of any installation folder with an accent in its name,
        /// and the command then silently started nothing at all.
        /// </summary>
        private static Encoding ScriptEncoding()
        {
            try
            {
                return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch (ArgumentException)
            {
                return Encoding.Default;
            }
            catch (NotSupportedException)
            {
                return Encoding.Default;
            }
        }

        /// <summary>
        /// The stored search path is read and written unexpanded and with its original value
        /// kind: reading it through the expanded managed view and writing it back would turn
        /// every "%USERPROFILE%" style entry in it into a fixed path.
        /// </summary>
        private static void JoinSearchPath()
        {
            string directory = BinDirectory;

            // This process first, and unconditionally: every shell started from here inherits
            // this environment, and that is how it finds the banner program by name. Doing it
            // only when the stored path changes left every later run without it.
            string live = Environment.GetEnvironmentVariable(PathValue) ?? string.Empty;
            if (!Contains(live, directory))
            {
                Environment.SetEnvironmentVariable(PathValue, live.TrimEnd(';') + ";" + directory,
                    EnvironmentVariableTarget.Process);
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(EnvironmentKey, true))
            {
                if (key == null)
                {
                    return;
                }

                string stored = key.GetValue(PathValue, string.Empty,
                    RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                stored = stored ?? string.Empty;

                if (Contains(stored, directory))
                {
                    return;
                }

                RegistryValueKind kind = stored.Length == 0
                    ? RegistryValueKind.ExpandString
                    : key.GetValueKind(PathValue);

                string joined = stored.Length == 0
                    ? directory
                    : stored.TrimEnd(';') + ";" + directory;

                key.SetValue(PathValue, joined, kind);
            }

            // Writing the value alone leaves every running program with the old environment
            // block. Setting a variable through the managed API announces the change to the
            // desktop, so a prompt opened afterwards finds the command.
            Environment.SetEnvironmentVariable(HomeVariable, directory, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(HomeVariable, directory, EnvironmentVariableTarget.Process);
        }

        private static bool Contains(string searchPath, string directory)
        {
            foreach (string entry in searchPath.Split(';'))
            {
                string trimmed = entry.Trim().Trim('"').TrimEnd(Path.DirectorySeparatorChar);
                if (trimmed.Length > 0 &&
                    string.Equals(trimmed, directory.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
