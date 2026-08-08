using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point32
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// What the window manager asks a window for before it maximises or resizes it. Filling in
    /// <see cref="MaxPosition"/> and <see cref="MaxSize"/> is the only way to stop a window with a
    /// custom frame from maximising over the taskbar and past every edge of the screen.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo
    {
        public Point32 Reserved;
        public Point32 MaxSize;
        public Point32 MaxPosition;
        public Point32 MinTrackSize;
        public Point32 MaxTrackSize;
    }
}
