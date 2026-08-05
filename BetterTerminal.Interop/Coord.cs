using System.Runtime.InteropServices;

namespace BetterTerminal.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct Coord
    {
        public short X;
        public short Y;
        // kernel32, wincontypes.h COORD: two SHORTs, packed as a single DWORD by value.

        public Coord(short x, short y)
        {
            X = x;
            Y = y;
        }
    }
}
