using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BetterTerminal.Interop;

namespace BetterTerminal.Terminal
{
    public sealed class ConsoleHwndHost : HwndHost
    {
        private const int WM_SETFOCUS = 0x0007;

        private readonly HwndConsoleSession _session;

        private IntPtr _hostWindow;
        private bool _attached;

        public ConsoleHwndHost(HwndConsoleSession session)
        {
            _session = session;
            _session.ConsoleWindowReady += OnConsoleWindowReady;
            Focusable = true;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            int style = unchecked((int)(ConsoleWindowStyles.WS_CHILD |
                                        ConsoleWindowStyles.WS_VISIBLE |
                                        ConsoleWindowStyles.WS_CLIPCHILDREN));

            _hostWindow = NativeMethods.CreateWindowEx(
                0,
                "static",
                null,
                style,
                0,
                0,
                Math.Max(1, (int)ActualWidth),
                Math.Max(1, (int)ActualHeight),
                hwndParent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_hostWindow == IntPtr.Zero)
            {
                Win32Error.Throw("CreateWindowEx");
            }

            if (_session.ConsoleWindowHandle != IntPtr.Zero)
            {
                AttachConsoleWindow();
            }

            return new HandleRef(this, _hostWindow);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            _session.ConsoleWindowReady -= OnConsoleWindowReady;

            if (_hostWindow != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hostWindow);
                _hostWindow = IntPtr.Zero;
            }

            _attached = false;
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SETFOCUS && _session.ConsoleWindowHandle != IntPtr.Zero)
            {
                NativeMethods.SetFocus(_session.ConsoleWindowHandle);
                handled = true;
                return IntPtr.Zero;
            }

            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            LayoutConsoleWindow();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            LayoutConsoleWindow();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_session.ConsoleWindowHandle != IntPtr.Zero)
            {
                NativeMethods.SetFocus(_session.ConsoleWindowHandle);
            }
        }

        public void FocusConsole()
        {
            if (_session.ConsoleWindowHandle != IntPtr.Zero)
            {
                NativeMethods.SetFocus(_session.ConsoleWindowHandle);
            }
        }

        private void OnConsoleWindowReady(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(AttachConsoleWindow));
        }

        private void AttachConsoleWindow()
        {
            IntPtr console = _session.ConsoleWindowHandle;
            if (_attached || console == IntPtr.Zero || _hostWindow == IntPtr.Zero)
            {
                return;
            }

            // SetParent returns the previous parent, which is NULL for a top-level console window,
            // so a zero result here is the expected success case and cannot be tested for failure.
            NativeMethods.SetParent(console, _hostWindow);

            long style = NativeMethods.GetWindowLongPtr(console, ConsoleWindowStyles.GWL_STYLE).ToInt64();
            style &= ~(ConsoleWindowStyles.WS_CAPTION |
                       ConsoleWindowStyles.WS_THICKFRAME |
                       ConsoleWindowStyles.WS_SYSMENU |
                       ConsoleWindowStyles.WS_MINIMIZEBOX |
                       ConsoleWindowStyles.WS_MAXIMIZEBOX |
                       ConsoleWindowStyles.WS_BORDER |
                       ConsoleWindowStyles.WS_DLGFRAME |
                       ConsoleWindowStyles.WS_POPUP);
            style |= ConsoleWindowStyles.WS_CHILD |
                     ConsoleWindowStyles.WS_VISIBLE |
                     ConsoleWindowStyles.WS_CLIPSIBLINGS;
            NativeMethods.SetWindowLongPtr(console, ConsoleWindowStyles.GWL_STYLE, (IntPtr)style);

            long exStyle = NativeMethods.GetWindowLongPtr(console, ConsoleWindowStyles.GWL_EXSTYLE).ToInt64();
            exStyle &= ~(ConsoleWindowStyles.WS_EX_CLIENTEDGE |
                         ConsoleWindowStyles.WS_EX_WINDOWEDGE |
                         ConsoleWindowStyles.WS_EX_DLGMODALFRAME |
                         ConsoleWindowStyles.WS_EX_STATICEDGE |
                         ConsoleWindowStyles.WS_EX_APPWINDOW);
            NativeMethods.SetWindowLongPtr(console, ConsoleWindowStyles.GWL_EXSTYLE, (IntPtr)exStyle);

            NativeMethods.SetWindowPos(
                console,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                ConsoleWindowStyles.SWP_NOMOVE |
                ConsoleWindowStyles.SWP_NOSIZE |
                ConsoleWindowStyles.SWP_NOZORDER |
                ConsoleWindowStyles.SWP_NOACTIVATE |
                ConsoleWindowStyles.SWP_FRAMECHANGED);

            NativeMethods.ShowWindow(console, ConsoleWindowStyles.SW_SHOWNA);

            _attached = true;
            LayoutConsoleWindow();
        }

        private void LayoutConsoleWindow()
        {
            IntPtr console = _session.ConsoleWindowHandle;
            if (!_attached || console == IntPtr.Zero)
            {
                return;
            }

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            int width = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
            int height = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));

            if (!NativeMethods.MoveWindow(console, 0, 0, width, height, true))
            {
                Win32Error.Throw("MoveWindow");
            }
        }
    }
}
