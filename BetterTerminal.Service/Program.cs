using System;
using System.ServiceProcess;

namespace BetterTerminal.Service
{
    /// <summary>
    /// The service program. Started by the service control manager with no arguments, it runs as a
    /// service. From an elevated prompt it also accepts "--install" and "--uninstall" to register
    /// and remove itself, and "--console" to run in the foreground for a quick check.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0
                ? args[0].TrimStart('-', '/').ToLowerInvariant()
                : string.Empty;

            switch (command)
            {
                case "install":
                    return Guarded(delegate
                    {
                        ServiceControl.Install();
                        Console.WriteLine("Installed the service " + HostService.Display + ".");
                    });

                case "uninstall":
                    return Guarded(delegate
                    {
                        ServiceControl.Uninstall();
                        Console.WriteLine("Removed the service " + HostService.Display + ".");
                    });

                case "console":
                    new HostService().RunConsole();
                    return 0;

                case "check":
                    return RunCheck();

                case "help":
                case "?":
                    Usage();
                    return 0;

                default:
                    ServiceBase.Run(new HostService());
                    return 0;
            }
        }

        private static int RunCheck()
        {
            System.Version staged = UpdateCheck.Run();
            System.Console.WriteLine(staged == null
                ? "No newer release is available."
                : "Staged update " + Updating.UpdateShared.NormalizedString(staged) + ".");
            return 0;
        }

        private static int Guarded(Action action)
        {
            try
            {
                action();
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                Console.Error.WriteLine(
                    "Installing or removing a service needs an elevated (Administrator) prompt.");
                return 1;
            }
        }

        private static void Usage()
        {
            Console.WriteLine(HostService.Display);
            Console.WriteLine("  --install     register the service (needs an elevated prompt)");
            Console.WriteLine("  --uninstall   remove the service (needs an elevated prompt)");
            Console.WriteLine("  --console     run in the foreground until Enter is pressed");
            Console.WriteLine("  --check       check for a newer release now and stage it if found");
        }
    }
}
