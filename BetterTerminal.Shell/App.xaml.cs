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

            // Registering the command is a no-op on every start after the first.
            CommandRegistration.Ensure();

            // Dark is the shipped default; the theme service resolves high contrast and the
            // Windows preference from here on.
            ThemeService.Current.Initialize(Resources);

            SplashWindow splash = new SplashWindow();
            splash.Show();

            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Loaded += delegate { splash.Close(); };
            window.Show();
        }
    }
}
