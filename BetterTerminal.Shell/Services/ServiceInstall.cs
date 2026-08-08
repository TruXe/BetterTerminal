using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Registers the background host as a Windows service on the first run.
    ///
    /// This is the one thing BetterTerminal does that leaves the user's profile, and it is the one
    /// thing that asks for administrator rights: a service lives in the machine's service database.
    /// It is therefore asked **once**. Whatever the answer - installed, refused, or failed - the
    /// attempt is written down and never repeated, because a prompt on every start would be worse
    /// than not having the service at all. Someone who said no can still install it by hand later
    /// with "beterm-service.exe --install" from an elevated prompt.
    /// </summary>
    public static class ServiceInstall
    {
        public const string ServiceName = "BetterTerminalHost";
        public const string ExecutableName = "beterm-service.exe";

        private const string MarkerName = "service-install.txt";

        /// <summary>
        /// Does the whole thing on a pool thread and returns at once: the elevation prompt is the
        /// user's to answer in their own time, and the window must not wait on it.
        /// </summary>
        public static void EnsureLater()
        {
            ThreadPool.QueueUserWorkItem(delegate { Ensure(); });
        }

        public static void Ensure()
        {
            try
            {
                if (IsInstalled() || WasAttempted())
                {
                    return;
                }

                string program = Path.Combine(SelfInstall.InstallDirectory, ExecutableName);
                if (!File.Exists(program))
                {
                    return;
                }

                MarkAttempted("asked");
                Install(program);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static bool IsInstalled()
        {
            try
            {
                foreach (ServiceController service in ServiceController.GetServices())
                {
                    using (service)
                    {
                        if (string.Equals(service.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The service database could not be read; treat that as "cannot tell" and leave it.
                return true;
            }
            catch (Win32Exception)
            {
                return true;
            }

            return false;
        }

        private static void Install(string program)
        {
            ProcessStartInfo start = new ProcessStartInfo(program, "--install");
            start.WorkingDirectory = Path.GetDirectoryName(program);
            start.UseShellExecute = true;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;

            // The only elevated thing the application ever starts. UseShellExecute has to stay on
            // for the verb to mean anything.
            start.Verb = "runas";

            try
            {
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return;
                    }

                    process.WaitForExit();
                    MarkAttempted(process.ExitCode == 0 ? "installed" : "failed " + process.ExitCode);
                }
            }
            catch (Win32Exception)
            {
                // The prompt was refused, or elevation is not available on this machine. Both are
                // an answer, and the marker written before the attempt keeps it from being asked again.
            }
        }

        private static string MarkerPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BetterTerminal",
                MarkerName);
        }

        private static bool WasAttempted()
        {
            return File.Exists(MarkerPath());
        }

        private static void MarkAttempted(string outcome)
        {
            try
            {
                string path = MarkerPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + outcome + Environment.NewLine +
                    "Delete this file to be asked again, or run \"" + ExecutableName +
                    " --install\" from an elevated prompt." + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
