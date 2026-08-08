using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    /// <summary>
    /// A rectangle as the window manager gives it: four edges, not a position and a size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width
        {
            get { return Right - Left; }
        }

        public int Height
        {
            get { return Bottom - Top; }
        }
    }

    /// <summary>
    /// What a monitor covers and what is left of it once the taskbar and any application bar have
    /// taken their share. <see cref="WorkArea"/> is the part a maximised window may use.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect32 Monitor;
        public Rect32 WorkArea;
        public uint Flags;
    }
}
