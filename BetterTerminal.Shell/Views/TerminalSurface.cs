using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// The live terminal inside one pane: owns the session and hosts whichever surface the
    /// backend needs. It carries no chrome of its own - the pane header, focus ring and close
    /// button belong to the pane DataTemplate in MainWindow.xaml.
    /// </summary>
    public sealed class TerminalSurface : ContentControl
    {
        private const int InitialColumns = 80;
        private const int InitialRows = 24;

        private readonly TerminalBackend _backend;

        private ITerminalSession _session;
        private TerminalRenderer _renderer;
        private ConsoleHwndHost _consoleHost;

        public TerminalSurface(ShellProfile shell, string workingDirectory, TerminalBackend backend)
        {
            Shell = shell;
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : workingDirectory;
            _backend = backend;

            Focusable = false;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;

            Loaded += OnLoaded;
        }

        public event EventHandler<TerminalTitleEventArgs> TitleChanged;

        public event EventHandler<TerminalExitEventArgs> Exited;

        public ShellProfile Shell { get; private set; }

        public string WorkingDirectory { get; private set; }

        /// <summary>
        /// A line sent to the session once it is running, as if the user had typed it. Input
        /// reaches the child only this way - it is never spliced into the child command line.
        /// Set before the surface is loaded; a restart runs it again.
        /// </summary>
        public string StartupCommand { get; set; }

        public bool IsRunning
        {
            get { return _session != null && _session.IsRunning; }
        }

        public void FocusTerminal()
        {
            if (_renderer != null)
            {
                _renderer.Focus();
            }
            else if (_consoleHost != null)
            {
                _consoleHost.FocusConsole();
            }
        }

        public void Write(string text)
        {
            if (_session != null && _session.IsRunning)
            {
                _session.Write(text);
            }
        }

        public void ApplyFont(string fontFamily, double fontSize)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.TerminalFontSize = fontSize;
            _renderer.SetFontFamily(fontFamily);
        }

        public void ApplyCaret(CaretShape shape, bool blinks)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.CaretShape = shape;
            _renderer.CaretBlinks = blinks;
            _renderer.Redraw();
        }

        public void ApplyColors(Color background, Color foreground, Color caret, Color selection)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.DefaultBackground = background;
            _renderer.DefaultForeground = foreground;
            _renderer.CaretColor = caret;
            _renderer.SelectionBackground = selection;
            _renderer.Redraw();
        }

        public void Restart()
        {
            CloseSession();
            StartSession();
        }

        public void CloseSession()
        {
            if (_session == null)
            {
                return;
            }

            if (_renderer != null)
            {
                _renderer.Detach();
            }

            _session.TitleChanged -= OnSessionTitleChanged;
            _session.Exited -= OnSessionExited;
            _session.Close();
            _session.Dispose();
            _session = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_session == null)
            {
                StartSession();
            }
        }

        private void StartSession()
        {
            _renderer = null;
            _consoleHost = null;

            _session = TerminalSessionFactory.Create(_backend, InitialColumns, InitialRows);
            _session.TitleChanged += OnSessionTitleChanged;
            _session.Exited += OnSessionExited;

            ConPtySession pseudoConsole = _session as ConPtySession;
            if (pseudoConsole != null)
            {
                _renderer = new TerminalRenderer();
                ApplyThemeColors();
                _renderer.Attach(pseudoConsole, pseudoConsole.Grid);
                Content = _renderer;
            }
            else
            {
                _consoleHost = new ConsoleHwndHost((HwndConsoleSession)_session);
                Content = _consoleHost;
            }

            _session.Start(Shell, WorkingDirectory);

            if (!string.IsNullOrEmpty(StartupCommand))
            {
                // The shell reads its input pipe when it is ready, so queueing the line right
                // after the start is enough and needs no readiness handshake.
                _session.Write(StartupCommand + "\r");
            }

            Dispatcher.BeginInvoke(new Action(FocusTerminal));
        }

        private void ApplyThemeColors()
        {
            _renderer.DefaultBackground = SchemeColor("Bt.Scheme.Background", Color.FromRgb(0x0C, 0x0C, 0x0C));
            _renderer.DefaultForeground = SchemeColor("Bt.Scheme.Foreground", Color.FromRgb(0xCC, 0xCC, 0xCC));
            _renderer.CaretColor = SchemeColor("Bt.Scheme.Cursor", Color.FromRgb(0xCC, 0xCC, 0xCC));
            _renderer.SelectionBackground = SchemeColor("Bt.Scheme.Selection", Color.FromRgb(0x3A, 0x3D, 0x41));
        }

        private static Color SchemeColor(string key, Color fallback)
        {
            if (Application.Current == null)
            {
                return fallback;
            }

            object value = Application.Current.TryFindResource(key);
            return value is Color ? (Color)value : fallback;
        }

        private void OnSessionTitleChanged(object sender, TerminalTitleEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                EventHandler<TerminalTitleEventArgs> handler = TitleChanged;
                if (handler != null)
                {
                    handler(this, e);
                }
            }));
        }

        private void OnSessionExited(object sender, TerminalExitEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                EventHandler<TerminalExitEventArgs> handler = Exited;
                if (handler != null)
                {
                    handler(this, e);
                }
            }));
        }
    }
}
