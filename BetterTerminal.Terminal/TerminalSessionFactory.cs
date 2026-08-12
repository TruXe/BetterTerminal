namespace BetterTerminal.Terminal
{
    public static class TerminalSessionFactory
    {
        // A million lines of history per pane. It costs nothing until it is used: CellGrid grows its
        // ring on demand and stores each line at the width actually written, so an idle pane holds a
        // few pages and a pane that has scrolled a million lines is the only one paying for them.
        public const int DefaultScrollbackLines = 1000000;

        public static ITerminalSession Create(TerminalBackend backend, int columns, int rows)
        {
            if (Resolve(backend) == TerminalBackend.PseudoConsole)
            {
                return new ConPtySession(columns, rows, DefaultScrollbackLines);
            }

            return new HwndConsoleSession();
        }

        public static TerminalBackend Resolve(TerminalBackend backend)
        {
            if (backend == TerminalBackend.Automatic)
            {
                return ConPtySession.IsSupported ? TerminalBackend.PseudoConsole : TerminalBackend.HostedConsoleWindow;
            }

            if (backend == TerminalBackend.PseudoConsole && !ConPtySession.IsSupported)
            {
                return TerminalBackend.HostedConsoleWindow;
            }

            return backend;
        }
    }
}
