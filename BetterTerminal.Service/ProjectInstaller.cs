using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace BetterTerminal.Service
{
    /// <summary>
    /// What the installer runs when the service is registered. It creates the service under the
    /// local system account, names it, and sets it to start with the machine. This is the class the
    /// self-install path drives; registering a service is a machine-wide operation and needs an
    /// elevated (Administrator) prompt, which is the deliberate exception to the application's
    /// otherwise per-user, never-elevate rule.
    /// </summary>
    [RunInstaller(true)]
    public sealed class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            ServiceProcessInstaller process = new ServiceProcessInstaller();
            process.Account = ServiceAccount.LocalSystem;

            ServiceInstaller service = new ServiceInstaller();
            service.ServiceName = HostService.Name;
            service.DisplayName = HostService.Display;
            service.Description = HostService.Description;
            service.StartType = ServiceStartMode.Automatic;

            Installers.Add(process);
            Installers.Add(service);
        }
    }
}
