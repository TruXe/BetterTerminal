using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BetterTerminal.Updating;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// The update notice, shown in the corner in the application's own style rather than as a system
    /// toast: a Windows toast is routed through the notification centre and is silently held back
    /// when another application is in the foreground, which is exactly when this needs to be seen.
    /// It dismisses itself after a while, and the countdown pauses while the pointer is over it so a
    /// reader is never cut off mid-sentence.
    /// </summary>
    public partial class UpdateToastWindow : Window
    {
        private const int VisibleSeconds = 10;
        private const double EdgeMargin = 8;

        private readonly Action _restart;
        private readonly DispatcherTimer _dismiss;

        public UpdateToastWindow(Version version, Action restart)
        {
            InitializeComponent();

            _restart = restart;
            Message.Text = "Version " + UpdateShared.NormalizedString(version) +
                " is ready. It installs when you restart BetterTerminal.";

            _dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(VisibleSeconds) };
            _dismiss.Tick += delegate { Close(); };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - EdgeMargin;
            Top = area.Bottom - ActualHeight - EdgeMargin;
            _dismiss.Start();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _dismiss.Stop();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _dismiss.Start();
        }

        private void OnRestart(object sender, RoutedEventArgs e)
        {
            _dismiss.Stop();

            Action restart = _restart;
            Close();
            if (restart != null)
            {
                restart();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
