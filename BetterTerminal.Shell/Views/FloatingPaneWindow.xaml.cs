using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using BetterTerminal.Interop;
using BetterTerminal.Shell.Services;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// One leaf living outside the pane grid. It holds the very element the grid was holding - the
    /// same surface, the same session, the same scrollback - so tearing off and docking back is a
    /// move, never a restart.
    ///
    /// The header is dragged by hand rather than by the window frame. Handing the pointer to the
    /// frame's move loop would block until the button came up, and the dock targets have to be
    /// hit-tested while the drag is still running.
    /// </summary>
    public partial class FloatingPaneWindow : Window
    {
        private DockController _controller;
        private DockLeafViewModel _leaf;

        private bool _dragging;
        private int _grabX;
        private int _grabY;
        private bool _keepContentOnClose;

        public FloatingPaneWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        /// <summary>The leaf this window is standing in for, or null once it has been given up.</summary>
        public DockLeafViewModel Leaf
        {
            get { return _leaf; }
        }

        public void Attach(DockController controller, DockLeafViewModel leaf)
        {
            _controller = controller;
            _leaf = leaf;

            Host.Content = leaf.Content;
            Caption.Text = leaf.HeaderText;
            Title = leaf.HeaderText;

            Header.PreviewMouseLeftButtonDown += OnHeaderMouseDown;
            Header.PreviewMouseMove += OnHeaderMouseMove;
            Header.PreviewMouseLeftButtonUp += OnHeaderMouseUp;
            Header.LostMouseCapture += OnHeaderLostCapture;
        }

        /// <summary>
        /// Hands the content back without closing anything. The caller owns the element afterwards
        /// and this window is finished - closing it must not reach the session.
        /// </summary>
        public void ReleaseContent()
        {
            Host.Content = null;
            _keepContentOnClose = true;
            _leaf = null;
        }

        /// <summary>Where this window sits, in physical screen pixels.</summary>
        public Rect32 Bounds
        {
            get
            {
                Rect32 rect;
                return NativeMethods.GetWindowRect(Handle, out rect) ? rect : new Rect32();
            }
        }

        public void MoveTo(Rect32 bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            NativeMethods.MoveWindow(Handle, bounds.Left, bounds.Top, bounds.Width, bounds.Height, true);
        }

        /// <summary>
        /// Picks the drag up where the pane header left it. The button is still down from the
        /// gesture that tore this window off, so capturing here lets the same press carry on
        /// without the user noticing a handover happened.
        /// </summary>
        public void BeginDragFromCursor()
        {
            Point32 cursor;
            Rect32 bounds;
            if (!NativeMethods.GetCursorPos(out cursor) || !NativeMethods.GetWindowRect(Handle, out bounds))
            {
                return;
            }

            _grabX = cursor.X - bounds.Left;
            _grabY = cursor.Y - bounds.Top;
            _dragging = true;

            Activate();
            Header.CaptureMouse();
            _controller.BeginFloatingDrag(this);
        }

        private IntPtr Handle
        {
            get { return new WindowInteropHelper(this).Handle; }
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // A double click on the header is the fastest way back into the grid.
                e.Handled = true;
                DockBack();
                return;
            }

            Point32 cursor;
            Rect32 bounds;
            if (!NativeMethods.GetCursorPos(out cursor) || !NativeMethods.GetWindowRect(Handle, out bounds))
            {
                return;
            }

            _grabX = cursor.X - bounds.Left;
            _grabY = cursor.Y - bounds.Top;
            _dragging = true;

            Header.CaptureMouse();
            _controller.BeginFloatingDrag(this);
            e.Handled = true;
        }

        private void OnHeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point32 cursor;
            Rect32 bounds;
            if (!NativeMethods.GetCursorPos(out cursor) || !NativeMethods.GetWindowRect(Handle, out bounds))
            {
                return;
            }

            NativeMethods.MoveWindow(
                Handle, cursor.X - _grabX, cursor.Y - _grabY, bounds.Width, bounds.Height, true);

            _controller.UpdateDrag(cursor);
        }

        private void OnHeaderMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            Header.ReleaseMouseCapture();

            Point32 cursor;
            if (NativeMethods.GetCursorPos(out cursor))
            {
                _controller.CommitDrag(cursor);
            }
            else
            {
                _controller.CancelDrag();
            }

            e.Handled = true;
        }

        private void OnHeaderLostCapture(object sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            // Something took the mouse away mid-drag; leave the window where it is rather than
            // docking it somewhere the user did not aim at.
            _dragging = false;
            _controller.CancelDrag();
        }

        private void OnDockBack(object sender, RoutedEventArgs e)
        {
            DockBack();
        }

        private void DockBack()
        {
            if (_controller != null && _leaf != null)
            {
                _controller.DockBack(this);
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_keepContentOnClose || _leaf == null)
            {
                return;
            }

            _controller.FloatingWindowClosed(this);
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
