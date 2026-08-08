using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterTerminal.Shell.Services;
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
        private const string VirtualItemFormat = "FileGroupDescriptorW";

        private readonly TerminalBackend _backend;

        private ITerminalSession _session;
        private TerminalRenderer _renderer;
        private ConsoleHwndHost _consoleHost;
        private int _dragDepth;
        private bool _isDropTarget;

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

            Background = Brushes.Transparent;
            AttachDropTarget();

            Loaded += OnLoaded;
        }

        public event EventHandler<TerminalTitleEventArgs> TitleChanged;

        public event EventHandler<TerminalExitEventArgs> Exited;

        public event EventHandler DropTargetChanged;

        public event EventHandler<PaneDropEventArgs> DropReported;

        public bool IsDropTarget
        {
            get { return _isDropTarget; }
        }

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

        /// <summary>
        /// Whether this surface can be moved to another top-level window with the session intact.
        /// The drawn surface can: it is ordinary WPF over a pseudo console, and re-parenting it
        /// keeps the process, the pseudo console and the scrollback exactly as they were. The
        /// fallback backend cannot: it hosts a real console window as a child of this one, and that
        /// child does not follow the element to a different window.
        /// </summary>
        public bool CanReparent
        {
            get { return _consoleHost == null; }
        }

        /// <summary>
        /// The drawn session behind this surface, or null on the hosted-console fallback backend.
        /// The local web server reads its grid and writes to it; only a pseudo console exposes a
        /// grid and an input pipe, which is why the fallback cannot be served to a browser.
        /// </summary>
        internal ConPtySession PseudoConsole
        {
            get { return _session as ConPtySession; }
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
            AttachDropTarget();
            StartSession();
        }

        public void CloseSession()
        {
            DetachDropTarget();

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

        private void AttachDropTarget()
        {
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
        }

        private void DetachDropTarget()
        {
            AllowDrop = false;
            DragEnter -= OnDragEnter;
            DragOver -= OnDragOver;
            DragLeave -= OnDragLeave;
            Drop -= OnDrop;

            _dragDepth = 0;
            SetDropTarget(false);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            _dragDepth++;
            ApplyEffects(e);
            SetDropTarget(e.Effects != DragDropEffects.None);
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            ApplyEffects(e);
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            _dragDepth--;
            if (_dragDepth <= 0)
            {
                _dragDepth = 0;
                SetDropTarget(false);
            }

            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            _dragDepth = 0;
            SetDropTarget(false);
            e.Handled = true;

            if (!CanAccept(e.Data))
            {
                return;
            }

            e.Effects = DragDropEffects.Copy;

            if (HasPaths(e.Data))
            {
                Insert(TextFor(e.Data));
                return;
            }

            Report("Those items have no file path yet. Save or copy them to a folder first.");
        }

        private static void ApplyEffects(DragEventArgs e)
        {
            e.Effects = CanAccept(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private static bool CanAccept(IDataObject data)
        {
            return data != null && (HasPaths(data) || data.GetDataPresent(VirtualItemFormat));
        }

        private static bool HasPaths(IDataObject data)
        {
            return data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.UnicodeText);
        }

        private string TextFor(IDataObject data)
        {
            PaneShellKind kind = DroppedPaths.KindOf(Shell, StartupCommand);

            string[] dropped = data.GetData(DataFormats.FileDrop) as string[];
            if (dropped != null && dropped.Length > 0)
            {
                return DroppedPaths.Format(dropped, kind);
            }

            string text = data.GetData(DataFormats.UnicodeText) as string;
            return DroppedPaths.Format(DroppedPaths.SplitLines(text), kind);
        }

        private void Insert(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(Insert), text);
                return;
            }

            if (_renderer == null || !IsRunning)
            {
                Report("This session does not take inserted text.");
                return;
            }

            _renderer.PasteText(text);
            Report(string.Empty);
            FocusTerminal();
        }

        private void Report(string message)
        {
            EventHandler<PaneDropEventArgs> handler = DropReported;
            if (handler != null)
            {
                handler(this, new PaneDropEventArgs(message));
            }
        }

        private void SetDropTarget(bool value)
        {
            if (_isDropTarget == value)
            {
                return;
            }

            _isDropTarget = value;

            EventHandler handler = DropTargetChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
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
