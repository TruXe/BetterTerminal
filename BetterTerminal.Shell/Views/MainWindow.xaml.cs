using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using BetterTerminal.Interop;
using BetterTerminal.Shell.Services;
using BetterTerminal.Shell.ViewModels;
using BetterTerminal.Terminal;

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
        private UpdateClient _updateClient;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _model;

            // This window draws its own frame, and a window that does is maximised past every edge
            // of the screen unless it says otherwise.
            WindowFrame.KeepInsideScreen(this);

            _workspace = new TerminalWorkspace(_model, this, Palette);

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            AllowDropsFromLowerIntegrityProcesses();
        }

        private void AllowDropsFromLowerIntegrityProcesses()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.ChangeWindowMessageFilterEx(handle, NativeMethods.WM_DROPFILES, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
            NativeMethods.ChangeWindowMessageFilterEx(handle, NativeMethods.WM_COPYDATA, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
            NativeMethods.ChangeWindowMessageFilterEx(handle, NativeMethods.WM_COPYGLOBALDATA, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _workspace.AttachDocking(PaneHost, Docking);
            _workspace.Restore();

            // Records the installed version for the service to compare against and listens for it to
            // report a staged build. Started after the window is up so nothing here delays the show.
            _updateClient = new UpdateClient(Dispatcher);
            _updateClient.Start();
        }

        // ===== pulling a pane out by its header =====
        //
        // The tear-off waits for the pointer to travel: a press on the header is also how a pane is
        // focused, and a pane that jumped into a window on every click would be unusable.

        private DockLeafViewModel _headerLeaf;
        private Point32 _headerOrigin;
        private bool _headerPressed;

        private void OnPaneHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement header = sender as FrameworkElement;
            if (header == null)
            {
                return;
            }

            _headerLeaf = header.DataContext as DockLeafViewModel;
            _headerPressed = _headerLeaf != null
                             && NativeMethods.GetCursorPos(out _headerOrigin);
        }

        private void OnPaneHeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (!_headerPressed || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point32 cursor;
            if (!NativeMethods.GetCursorPos(out cursor))
            {
                return;
            }

            if (Math.Abs(cursor.X - _headerOrigin.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(cursor.Y - _headerOrigin.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DockLeafViewModel leaf = _headerLeaf;
            _headerPressed = false;
            _headerLeaf = null;

            _workspace.Docking.TearOffAndDrag(leaf, cursor);
        }

        private void OnPaneHeaderMouseUp(object sender, MouseButtonEventArgs e)
        {
            _headerPressed = false;
            _headerLeaf = null;
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_updateClient != null)
            {
                _updateClient.Stop();
            }

            _workspace.Save();
            _workspace.Docking.CloseAllFloating();
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
