using System.Text;
using BetterTerminal.Terminal;

namespace BetterTerminal.Tests
{
    public static class DetectionTests
    {
        public static void Run(TestRun run)
        {
            run.Section("Addresses found in printed text");

            Plain(run);
            TrailingPunctuation(run);
            Parentheses(run);
            Wrapped(run);
            Ipv6(run);
            BareHost(run);
            NotDetected(run);
            SwitchedOff(run);
        }

        private static void Plain(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(80, 5, 100);
            writer.Write("2026/08/15 15:22:52 bettertranslator: serving http://127.0.0.1:8682/");

            TerminalLink link = writer.LinkAt(0, 50);
            run.Equal("a printed address becomes a link", "http://127.0.0.1:8682/",
                link == null ? null : link.Uri);
            run.Check("the link is one the terminal found",
                link != null && link.Origin == TerminalLinkOrigin.Detected);
            run.Check("the link covers the cells the address is printed in", CellsMatch(writer, link));
        }

        private static void TrailingPunctuation(TestRun run)
        {
            TerminalWriter sentence = new TerminalWriter(80, 5, 100);
            sentence.Write("see https://example.com/page.");
            TerminalLink dotted = sentence.LinkAt(0, 10);
            run.Equal("a full stop is left out of the link", "https://example.com/page",
                dotted == null ? null : dotted.Uri);

            TerminalWriter colon = new TerminalWriter(120, 5, 100);
            colon.Write("release: get https://api.github.com/repos/Owner/Name/releases/latest: status 404");
            TerminalLink trimmed = colon.LinkAt(0, 20);
            run.Equal("a colon before the next word is left out of the link",
                "https://api.github.com/repos/Owner/Name/releases/latest",
                trimmed == null ? null : trimmed.Uri);
        }

        private static void Parentheses(TestRun run)
        {
            TerminalWriter balanced = new TerminalWriter(80, 5, 100);
            balanced.Write("https://en.wikipedia.org/wiki/Terminal_(computing)");
            TerminalLink kept = balanced.LinkAt(0, 10);
            run.Equal("a bracket that closes one inside the address is kept",
                "https://en.wikipedia.org/wiki/Terminal_(computing)", kept == null ? null : kept.Uri);

            TerminalWriter wrapped = new TerminalWriter(80, 5, 100);
            wrapped.Write("(https://example.com/page)");
            TerminalLink dropped = wrapped.LinkAt(0, 10);
            run.Equal("a bracket that closes one outside the address is dropped",
                "https://example.com/page", dropped == null ? null : dropped.Uri);
        }

        private static void Wrapped(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(20, 6, 100);
            string uri = "https://example.com/a/very/long/path/that/wraps";
            writer.Write("go " + uri);

            TerminalLink link = writer.LinkAt(0, 5);
            run.Equal("an address that runs onto the next row is one link", uri,
                link == null ? null : link.Uri);
            run.Check("the wrapped link covers more than one row",
                link != null && link.Ranges.Count > 1);
            run.Check("the wrapped link covers the cells it is printed in", CellsMatch(writer, link));

            TerminalLink fromSecondRow = writer.LinkAt(1, 2);
            run.Check("the second row of the wrapped link finds the same link",
                fromSecondRow != null && link != null && fromSecondRow.Id == link.Id);
        }

        private static void Ipv6(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(80, 5, 100);
            writer.Write("listening on http://[fe80::1]:8080/status?live=1#top now");

            TerminalLink link = writer.LinkAt(0, 20);
            run.Equal("a bracketed host, its port, its query and its fragment stay in the link",
                "http://[fe80::1]:8080/status?live=1#top", link == null ? null : link.Uri);
        }

        private static void BareHost(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(80, 5, 100);
            writer.Write("try www.example.com/docs for more");

            TerminalLink link = writer.LinkAt(0, 8);
            run.Equal("a bare host is opened over a secure connection",
                "https://www.example.com/docs", link == null ? null : link.Uri);
        }

        private static void NotDetected(TestRun run)
        {
            TerminalWriter host = new TerminalWriter(80, 5, 100);
            host.Write("serving on 127.0.0.1:8682 now");
            run.Check("a host and port with no scheme is not a link", host.LinkAt(0, 14) == null);

            TerminalWriter path = new TerminalWriter(80, 5, 100);
            path.Write("wrote C:\\Users\\Someone\\notes.txt");
            run.Check("a file path is not a link", path.LinkAt(0, 10) == null);

            TerminalWriter word = new TerminalWriter(80, 5, 100);
            word.Write("xhttps://example.com");
            run.Check("an address glued to a word is not a link", word.LinkAt(0, 10) == null);
        }

        private static void SwitchedOff(TestRun run)
        {
            TerminalWriter writer = new TerminalWriter(80, 5, 100);
            writer.Write("serving http://127.0.0.1:8682/");

            TerminalLink link = writer.Grid.Links.Find(writer.Grid, 0, 12, 0, writer.Grid.TotalLines, false);
            run.Check("with the search switched off nothing in plain text is a link", link == null);
        }

        public static bool CellsMatch(TerminalWriter writer, TerminalLink link)
        {
            if (link == null)
            {
                return false;
            }

            StringBuilder text = new StringBuilder();
            foreach (TerminalLinkRange range in link.Ranges)
            {
                for (int column = range.Start; column < range.End; column++)
                {
                    TerminalCell cell = writer.CellAt(range.Line, column);
                    if ((cell.Flags & CellFlags.WideTrailing) == 0)
                    {
                        text.Append(cell.Character);
                    }
                }
            }

            return text.ToString() == link.Uri;
        }
    }
}
