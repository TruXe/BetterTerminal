namespace BetterTerminal.Terminal
{
    public sealed class TerminalLinkResult
    {
        private TerminalLinkResult(TerminalLinkOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message;
        }

        public TerminalLinkOutcome Outcome { get; private set; }

        public string Message { get; private set; }

        internal static TerminalLinkResult Opened()
        {
            return new TerminalLinkResult(TerminalLinkOutcome.Opened, string.Empty);
        }

        internal static TerminalLinkResult Refused(string message)
        {
            return new TerminalLinkResult(TerminalLinkOutcome.Refused, message);
        }

        internal static TerminalLinkResult Cancelled()
        {
            return new TerminalLinkResult(TerminalLinkOutcome.Cancelled, string.Empty);
        }

        internal static TerminalLinkResult Failed(string message)
        {
            return new TerminalLinkResult(TerminalLinkOutcome.Failed, message);
        }
    }
}
