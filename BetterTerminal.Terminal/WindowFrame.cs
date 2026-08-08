using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using BetterTerminal.Interop;

namespace BetterTerminal.Terminal
{
    /// <summary>
    /// Keeps a window with a custom frame inside the screen when it is maximised.
    ///
    /// A window that draws its own frame is maximised by the window manager to the whole monitor
    /// *inflated by the resize border* - eight pixels past every edge on a normal display. The
    /// title bar loses its left edge and the status strip disappears under the taskbar.
    ///
    /// Two halves, and both are needed. The window manager is answered with the monitor's work
    /// area, which is the correct thing to say. It does not settle it on its own here: whatever
    /// this window asks for, the frame is added on top of it again - measured repeatedly, with the
    /// answer given both before and after the frame's own, and marked handled and not. So the
    /// content is also inset by that border while maximised, which is the half that is fully under
    /// this application's control and is what the user actually sees.
    ///
    /// It lives here rather than in the application because every P/Invoke in this repository lives
    /// behind the interop assembly, and the shell does not reference it directly.
    /// </summary>
    public static class WindowFrame
    {
        /// <summary>
        /// Attach once, at construction. Does nothing at all if the platform refuses to answer,
        /// which leaves the old behaviour rather than a broken window.
        ///
        /// Deliberately hooked on Loaded and not on SourceInitialized: the custom frame installs
        /// its own hook for this message when the source appears, and hooks answer in the order
        /// they were added, so one attached earlier is simply overwritten by it. Being last is the
        /// whole trick - measured, not assumed.
        /// </summary>
        public static void KeepInsideScreen(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (window.IsLoaded)
            {
                Hook(window);
            }
            else
            {
                window.Loaded += delegate { Hook(window); };
            }

            window.StateChanged += delegate { Inset(window); };
            Inset(window);
        }

        /// <summary>
        /// Pulls the content in by the frame border while the window is maximised.
        ///
        /// Answering the window manager is not enough on its own: whatever this window asks for, it
        /// is maximised to the work area grown by the sizing border on every side - measured, three
        /// times, on this machine. The window really is larger than the screen, so no arrangement
        /// inside it can be blamed and none can fix it. What can be fixed is where the content
        /// sits: inset by exactly that border, the title bar, the panes and the status strip line
        /// up with the edges of the screen and the strip stops disappearing under the taskbar.
        /// </summary>
        private static void Inset(Window window)
        {
            FrameworkElement root = window.Content as FrameworkElement;
            if (root == null)
            {
                return;
            }

            if (window.WindowState != WindowState.Maximized)
            {
                root.Margin = new Thickness(0);
                return;
            }

            double scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
            if (scale <= 0)
            {
                scale = 1;
            }

            double x = (NativeMethods.GetSystemMetrics(NativeMethods.SmSizeFrameWidth) +
                        NativeMethods.GetSystemMetrics(NativeMethods.SmPaddedBorder)) / scale;
            double y = (NativeMethods.GetSystemMetrics(NativeMethods.SmSizeFrameHeight) +
                        NativeMethods.GetSystemMetrics(NativeMethods.SmPaddedBorder)) / scale;

            root.Margin = new Thickness(x, y, x, y);
        }

        private static void Hook(Window window)
        {
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            if (source != null)
            {
                source.AddHook(OnMessage);
            }
        }

        private static IntPtr OnMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != NativeMethods.WmGetMinMaxInfo)
            {
                return IntPtr.Zero;
            }

            IntPtr monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            MonitorInfo info = new MonitorInfo();
            info.Size = Marshal.SizeOf(typeof(MonitorInfo));
            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                return IntPtr.Zero;
            }

            MinMaxInfo limits = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));

            // Everything is relative to the monitor, not to the desktop: on a second screen the
            // work area does not start at zero, and the window manager expects the offset.
            limits.MaxPosition.X = info.WorkArea.Left - info.Monitor.Left;
            limits.MaxPosition.Y = info.WorkArea.Top - info.Monitor.Top;
            limits.MaxSize.X = info.WorkArea.Width;
            limits.MaxSize.Y = info.WorkArea.Height;

            // Without these the window can still be dragged larger than the work area once it is
            // maximised - snapping to the top edge takes this path too.
            limits.MaxTrackSize.X = info.WorkArea.Width;
            limits.MaxTrackSize.Y = info.WorkArea.Height;

            Marshal.StructureToPtr(limits, lParam, true);

            // The message is answered, not consumed: the custom frame reads it after this and
            // hands back the defaults again if it is told the question was already settled.
            return IntPtr.Zero;
        }
    }
}
