using BetterTerminal.Terminal;

namespace BetterTerminal.Tests
{
    public static class HyperlinkTests
    {
        private static readonly string Escape = ((char)0x1b).ToString();
        private static readonly string Bell = ((char)0x07).ToString();
        private static readonly string StringTerminator = ((char)0x1b).ToString() + (char)0x5c;

        public static void Run(TestRun run)
        {
            run.Section("Links a program declares for itself");

            BellTerminator(run);
            EscapeTerminator(run);
            Grouping(run);
            Malformed(run);
            Unterminated(run);
            Closing(run);
            DeclaredWins(run);
        }

        private static void BellTerminator(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 100);
            writer.Write(Open(string.Empty, "https://example.com/one", Bell) + "click here" + Close(Bell));

            TerminalLink link = writer.LinkAt(0, 3);
            run.Equal("bell terminator leaves only the visible text", "click here", writer.RowText(0));
            run.Check("bell terminator makes a link", link != null);
            run.Equal("bell terminator keeps the target", "https://example.com/one", link == null ? null : link.Uri);
            run.Check("a declared link says so",
                link != null && link.Origin == TerminalLinkOrigin.Declared);
        }

        private static void EscapeTerminator(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 100);
            writer.Write(Open(string.Empty, "https://example.com/two", StringTerminator)
                + "click here" + Close(StringTerminator));

            TerminalLink link = writer.LinkAt(0, 3);
            run.Equal("string terminator leaves only the visible text", "click here", writer.RowText(0));
            run.Equal("string terminator keeps the target", "https://example.com/two", link == null ? null : link.Uri);
        }

        private static void Grouping(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 100);
            string uri = "https://example.com/grouped";
            writer.Write(Open("id=7", uri, Bell) + "one" + Close(Bell)
                + " "
                + Open("id=7", uri, Bell) + "two" + Close(Bell));

            TerminalLink first = writer.LinkAt(0, 0);
            TerminalLink second = writer.LinkAt(0, 5);

            run.Equal("both runs are on screen", "one two", writer.RowText(0));
            run.Check("both runs are links", first != null && second != null);
            run.Check("the same id joins two runs into one link",
                first != null && second != null && first.Id == second.Id);
            run.Equal("the joined link keeps both runs", 2, first == null ? 0 : first.Ranges.Count);
        }

        private static void Malformed(TestRun run)
        {
            TerminalWriter missing = new TerminalWriter(40, 5, 100);
            missing.Write(Escape + "]8;https://evil.example" + Bell + "text");
            run.Equal("a link with no separator prints nothing of itself", "text", missing.RowText(0));
            run.Check("a link with no separator makes no link", missing.LinkAt(0, 1) == null);

            TerminalWriter bare = new TerminalWriter(40, 5, 100);
            bare.Write(Escape + "]8" + Bell + "text");
            run.Equal("a truncated link sequence prints nothing of itself", "text", bare.RowText(0));

            TerminalWriter empty = new TerminalWriter(40, 5, 100);
            empty.Write(Open(string.Empty, string.Empty, Bell) + "text");
            run.Equal("a link with no target prints nothing of itself", "text", empty.RowText(0));
            run.Check("a link with no target makes no link", empty.LinkAt(0, 1) == null);
        }

        private static void Unterminated(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 100);
            writer.Write(Escape + "]8;;https://example.com/never-ends");
            run.Equal("an unterminated link puts nothing on the grid", string.Empty, writer.RowText(0));
        }

        private static void Closing(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(40, 5, 100);
            writer.Write(Open(string.Empty, "https://example.com/three", Bell) + "abc" + Close(Bell) + "def");

            run.Check("text before the close is linked", writer.CellAt(0, 1).LinkId != 0);
            run.Check("text after the close is not linked", writer.CellAt(0, 4).LinkId == 0);
        }

        private static void DeclaredWins(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(60, 5, 100);
            writer.Write(Open(string.Empty, "https://target.example/real", Bell)
                + "https://shown.example" + Close(Bell));

            TerminalLink link = writer.LinkAt(0, 4);
            run.Equal("a declared link beats one found in the text",
                "https://target.example/real", link == null ? null : link.Uri);
        }

        private static string Open(string parameters, string uri, string terminator)
        {
            return Escape + "]8;" + parameters + ";" + uri + terminator;
        }

        private static string Close(string terminator)
        {
            return Escape + "]8;;" + terminator;
        }
    }
}
