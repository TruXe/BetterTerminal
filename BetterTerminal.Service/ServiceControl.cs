using System;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using Microsoft.Win32;

namespace BetterTerminal.Service
{
    /// <summary>
    /// Installs, upgrades and removes the service by running this assembly's own installer classes -
    /// the same thing the framework's InstallUtil does, without the separate tool. Both operations
    /// write to the machine's service database and therefore need an elevated prompt.
    ///
    /// Install is also the upgrade path. A running service locks its own binary, so an ordinary
    /// application update cannot replace <c>beterm-service.exe</c> and the service would otherwise
    /// stay at whatever version first registered it. To get past that, the fresh build is shipped a
    /// second time as <c>beterm-service-update.exe</c> - a copy nothing ever runs as a service, so it
    /// is never locked. Running <em>that</em> file with --install stops the old service (which frees
    /// the canonical file), copies the fresh bits onto <c>beterm-service.exe</c>, registers that
    /// canonical path and starts it. The service therefore always runs from the same stable path, and
    /// the update binary is only ever a transient installer.
    /// </summary>
    internal static class ServiceControl
    {
        /// <summary>The stable path the service is always registered and run from.</summary>
        private const string CanonicalName = "beterm-service.exe";

        public static void Install()
        {
            string running = ExecutablePath();
            string canonical = Path.Combine(Path.GetDirectoryName(running), CanonicalName);

            // Clear any existing registration first: stop the old service so its file unlocks, then
            // remove it, so the fresh binary can take the canonical path and be registered cleanly.
            if (Exists())
            {
                StopIfRunning();
                TryUninstall(running);
                WaitUntilGone(TimeSpan.FromSeconds(15));
            }

            // When install is driven from the separate update binary, the canonical file is now
            // unlocked and is replaced with the fresh bits. A first install runs from the canonical
            // file itself, so there is nothing to copy.
            if (!string.Equals(running, canonical, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(running, canonical, true);
            }

            ManagedInstallerClass.InstallHelper(new[] { canonical });

            // The managed installer records the image path from the assembly it loaded, and because
            // the update binary and the canonical file are byte-identical the loader hands back the
            // already-loaded update binary - so the registration would point at beterm-service-update
            // .exe and lock it, defeating the whole never-locked scheme. Pin the image path back to the
            // canonical file so the service always runs from there and the update binary stays free.
            SetImagePath(canonical);
            StartService();
        }

        private static void SetImagePath(string canonical)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\" + HostService.Name, true))
                {
                    if (key != null)
                    {
                        key.SetValue("ImagePath", "\"" + canonical + "\"", RegistryValueKind.ExpandString);
                    }
                }
            }
            catch (Exception)
            {
                // Best effort: if the image path could not be corrected the service still runs from
                // wherever it was registered; the next upgrade attempt corrects it again.
            }
        }

        public static void Uninstall()
        {
            StopIfRunning();
            ManagedInstallerClass.InstallHelper(new[] { "/u", ExecutablePath() });
        }

        private static string ExecutablePath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        private static bool Exists()
        {
            foreach (ServiceController service in ServiceController.GetServices())
            {
                using (service)
                {
                    if (string.Equals(service.ServiceName, HostService.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void StopIfRunning()
        {
            try
            {
                using (ServiceController service = new ServiceController(HostService.Name))
                {
                    if (service.Status != ServiceControllerStatus.Stopped &&
                        service.Status != ServiceControllerStatus.StopPending)
                    {
                        service.Stop();
                    }

                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            }
            catch (Exception)
            {
                // Already stopped, gone, or not stoppable in time; the caller carries on regardless.
            }
        }

        private static void StartService()
        {
            try
            {
                using (ServiceController service = new ServiceController(HostService.Name))
                {
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch (Exception)
            {
                // The service is registered to start automatically, so a start that could not be
                // waited out here still comes up on its own.
            }
        }

        private static void TryUninstall(string exe)
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { "/u", exe });
            }
            catch (Exception)
            {
                // The registration could not be removed through the managed installer; WaitUntilGone
                // decides whether it cleared anyway before the install is attempted.
            }
        }

        private static void WaitUntilGone(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (Exists() && DateTime.UtcNow < deadline)
            {
                System.Threading.Thread.Sleep(250);
            }
        }
    }
}
