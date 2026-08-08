using System.Windows;
using BetterTerminal.Notifications;
using BetterTerminal.Shell.Services;
using BetterTerminal.Shell.Views;

namespace BetterTerminal.Shell
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // The command shim passes the directory it was invoked from; parse it before any
            // window exists, because the first window decides what to open.
            StartupOptions.Current.Parse(e.Args);

            // Started by the service only to raise a notification: this application is the host that
            // loads the notification library and hands it the rest of the command line. A service in
            // session 0 cannot draw a window itself, and the library's window shows even when Windows
            // notifications are turned off. Nothing else opens; the application ends with the toast.
            if (StartupOptions.Current.HasNotify)
            {
                NotificationHost.Run(e.Args);
                return;
            }

            // Before anything is shown: if the service staged a newer build while this was closed,
            // hand off to it now, with no window and no session in the way of the file replacement.
            if (UpdateApply.TryApplyOnStartup())
            {
                Shutdown();
                return;
            }

            // Registering the command is a no-op on every start after the first.
            CommandRegistration.Ensure();

            // Dark is the shipped default; the theme service resolves high contrast and the
            // Windows preference from here on.
            ThemeService.Current.Initialize(Resources);

            SplashWindow splash = new SplashWindow();
            splash.Show();

            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Loaded += delegate
            {
                splash.Close();

                // After the window is up, never before: registering the service asks for
                // administrator rights, and that prompt must not stand in front of a window that
                // has not finished showing. Asked once in the life of the installation.
                ServiceInstall.EnsureLater();
            };
            window.Show();
        }
    }
}
