namespace BetterTerminal.Terminal
{
    public sealed class TerminalLinkPrompt
    {
        public TerminalLinkPrompt(string uri, string display, string text)
        {
            Uri = uri;
            Display = display;
            Text = text;
        }

        public string Uri { get; private set; }

        public string Display { get; private set; }

        public string Text { get; private set; }
    }
}
