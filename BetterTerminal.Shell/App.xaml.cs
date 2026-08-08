using System.Windows;
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
