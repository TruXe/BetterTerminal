namespace BetterTerminal.Terminal
{
    public static class TerminalSessionFactory
    {
        public const int DefaultScrollbackLines = 5000;

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
