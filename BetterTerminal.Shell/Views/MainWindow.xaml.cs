using System.ComponentModel;
using System.Windows;
using BetterTerminal.Shell.Services;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// View-only concerns (BP-R22). Caption buttons are the one place a window needs
    /// code-behind under WindowChrome; everything else binds to MainViewModel commands,
    /// which TerminalWorkspace implements.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _model = new MainViewModel();
        private readonly TerminalWorkspace _workspace;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _model;
            _workspace = new TerminalWorkspace(_model, this, Palette);

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _workspace.Restore();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _workspace.Save();
            _workspace.CloseAllSessions();
        }

        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnToggleMaximize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
