using System;
using System.ComponentModel;
using System.Windows;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// One session on its own, outside the pane grid. It owns the surface it is given and closes
    /// the session when the window closes - the same order the pane path uses.
    /// </summary>
    public partial class SessionWindow : Window
    {
        private TerminalSurface _surface;

        public SessionWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        public void Attach(TerminalSurface surface, string caption)
        {
            _surface = surface;
            Host.Content = surface;
            Caption.Text = caption;
            Title = caption;

            Loaded += delegate { Dispatcher.BeginInvoke(new Action(surface.FocusTerminal)); };
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_surface != null)
            {
                _surface.CloseSession();
                _surface = null;
            }
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
