using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using BetterTerminal.Updating;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Registers the background host as a Windows service on the first run, and keeps it current
    /// afterwards.
    ///
    /// Registering a service is the one thing BetterTerminal does that leaves the user's profile and
    /// the one thing that asks for administrator rights: a service lives in the machine's service
    /// database. The first install is therefore asked **once** - installed, refused or failed, the
    /// attempt is written down and never repeated, because a prompt on every start would be worse than
    /// no service at all.
    ///
    /// Upgrades are separate. A running service locks its own binary, so an ordinary update cannot
    /// replace it and the service would stay at whatever version first registered it. The fresh build
    /// ships a second, never-locked copy as <c>beterm-service-update.exe</c>; when it is newer than the
    /// registered service, this runs it elevated to take over (see ServiceControl). That is asked once
    /// per newer version, so a machine is not nagged, but a genuinely newer build is still offered.
    /// </summary>
    public static class ServiceInstall
    {
        public const string ServiceName = "BetterTerminalHost";
        public const string ExecutableName = "beterm-service.exe";

        /// <summary>The never-locked copy of the same build, run only to install or upgrade.</summary>
        public const string UpdateExecutableName = "beterm-service-update.exe";

        private const string MarkerName = "service-install.txt";
        private const string UpgradeMarkerName = "service-upgrade.txt";

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
                if (!IsInstalled())
                {
                    FirstInstall();
                    return;
                }

                UpgradeIfOutdated();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void FirstInstall()
        {
            if (WasAttempted())
            {
                return;
            }

            string program = Path.Combine(SelfInstall.InstallDirectory, ExecutableName);
            if (!File.Exists(program))
            {
                return;
            }

            MarkAttempted("asked");
            int code = RunElevatedInstall(program);
            if (code >= 0)
            {
                MarkAttempted(code == 0 ? "installed" : "failed " + code);
            }
        }

        /// <summary>
        /// If the shipped update binary is newer than the registered service, run it elevated to take
        /// over. Asked once per newer version so a refusal is not re-prompted every start; a later,
        /// newer build raises the version and asks again.
        /// </summary>
        private static void UpgradeIfOutdated()
        {
            string directory = SelfInstall.InstallDirectory;
            string current = Path.Combine(directory, ExecutableName);
            string fresh = Path.Combine(directory, UpdateExecutableName);
            if (!File.Exists(current) || !File.Exists(fresh))
            {
                return;
            }

            Version installed = UpdateShared.FileVersion(current);
            Version available = UpdateShared.FileVersion(fresh);
            if (installed == null || available == null || available <= installed)
            {
                return;
            }

            if (AlreadyOfferedUpgrade(available))
            {
                return;
            }

            MarkUpgradeOffered(available);
            RunElevatedInstall(fresh);
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

        /// <summary>Runs a service binary's --install elevated and returns its exit code, or -1 when
        /// the elevation prompt was refused or elevation is not available.</summary>
        private static int RunElevatedInstall(string program)
        {
            ProcessStartInfo start = new ProcessStartInfo(program, "--install");
            start.WorkingDirectory = Path.GetDirectoryName(program);
            start.UseShellExecute = true;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;

            // The only elevated thing the application ever starts. UseShellExecute has to stay on for
            // the verb to mean anything.
            start.Verb = "runas";

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
            catch (Win32Exception)
            {
                // The prompt was refused, or elevation is not available on this machine.
                return -1;
            }
        }

        private static string MarkerPath(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BetterTerminal",
                name);
        }

        private static bool WasAttempted()
        {
            return File.Exists(MarkerPath(MarkerName));
        }

        private static void MarkAttempted(string outcome)
        {
            Write(MarkerPath(MarkerName),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + outcome + Environment.NewLine +
                "Delete this file to be asked again, or run \"" + ExecutableName +
                " --install\" from an elevated prompt." + Environment.NewLine);
        }

        private static bool AlreadyOfferedUpgrade(Version version)
        {
            try
            {
                string path = MarkerPath(UpgradeMarkerName);
                if (!File.Exists(path))
                {
                    return false;
                }

                Version offered;
                return Version.TryParse(File.ReadAllText(path).Trim(), out offered) &&
                    UpdateShared.Normalize(offered) >= UpdateShared.Normalize(version);
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

        private static void MarkUpgradeOffered(Version version)
        {
            Write(MarkerPath(UpgradeMarkerName), UpdateShared.Normalize(version).ToString(4));
        }

        private static void Write(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, content);
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
