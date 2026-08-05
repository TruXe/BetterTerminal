namespace BetterTerminal.Interop
{
    public static class ConsoleWindowStyles
    {
        // user32, winuser.h window style bits used when a console window is reparented into a WPF host.
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const long WS_CHILD = 0x40000000L;
        public const long WS_VISIBLE = 0x10000000L;
        public const long WS_CLIPCHILDREN = 0x02000000L;
        public const long WS_CLIPSIBLINGS = 0x04000000L;
        public const long WS_CAPTION = 0x00C00000L;
        public const long WS_THICKFRAME = 0x00040000L;
        public const long WS_SYSMENU = 0x00080000L;
        public const long WS_MINIMIZEBOX = 0x00020000L;
        public const long WS_MAXIMIZEBOX = 0x00010000L;
        public const long WS_BORDER = 0x00800000L;
        public const long WS_DLGFRAME = 0x00400000L;
        public const long WS_POPUP = unchecked((long)0x80000000L);

        public const long WS_EX_CLIENTEDGE = 0x00000200L;
        public const long WS_EX_WINDOWEDGE = 0x00000100L;
        public const long WS_EX_DLGMODALFRAME = 0x00000001L;
        public const long WS_EX_STATICEDGE = 0x00020000L;
        public const long WS_EX_APPWINDOW = 0x00040000L;

        public const int SW_SHOWNA = 8;

        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
    }
}
