using System.Configuration.Install;
using System.Reflection;

namespace BetterTerminal.Service
{
    /// <summary>
    /// Installs and removes the service by running this assembly's own installer classes - the same
    /// thing the framework's InstallUtil does, without the separate tool. Both operations write to
    /// the machine's service database and therefore need an elevated prompt.
    /// </summary>
    internal static class ServiceControl
    {
        public static void Install()
        {
            ManagedInstallerClass.InstallHelper(new[] { ExecutablePath() });
        }

        public static void Uninstall()
        {
            ManagedInstallerClass.InstallHelper(new[] { "/u", ExecutablePath() });
        }

        private static string ExecutablePath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }
    }
}
