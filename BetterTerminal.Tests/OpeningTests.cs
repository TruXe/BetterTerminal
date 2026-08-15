using System.Collections.Generic;
using BetterTerminal.Terminal;

namespace BetterTerminal.Tests
{
    public static class OpeningTests
    {
        public static void Run(TestRun run)
        {
            run.Section("Opening a link");

            Allowed(run);
            Refused(run);
            Punycode(run);
            NonAscii(run);
            Mismatch(run);
            Elision(run);
        }

        private static void Allowed(TestRun run)
        {
            List<string> handed = new List<string>();
            TerminalLinkOpener opener = Opener(handed, true);

            TerminalLinkResult result = opener.Open(
                new TerminalLink(1, "https://example.com/page", TerminalLinkOrigin.Detected),
                new TerminalLinkOptions());

            run.Check("an allowed address opens", result.Outcome == TerminalLinkOutcome.Opened);
            run.Equal("the address is handed over exactly as it was found", "https://example.com/page",
                handed.Count == 1 ? handed[0] : null);
        }

        private static void Refused(TestRun run)
        {
            foreach (string uri in new[]
            {
                "javascript:alert(1)",
                "ftp://example.com/file",
                "vscode://open",
                "notascheme"
            })
            {
                List<string> handed = new List<string>();
                TerminalLinkOpener opener = Opener(handed, true);

                TerminalLinkResult result = opener.Open(
                    new TerminalLink(1, uri, TerminalLinkOrigin.Detected),
                    new TerminalLinkOptions());

                run.Check("an address that is not on the list is refused: " + uri,
                    result.Outcome == TerminalLinkOutcome.Refused);
                run.Check("a refused address never reaches the system: " + uri, handed.Count == 0);
            }

            List<string> none = new List<string>();
            TerminalLinkOptions unconfirmed = new TerminalLinkOptions();
            unconfirmed.ConfirmMisleading = false;

            TerminalLinkResult refusal = Opener(none, true).Open(
                new TerminalLink(1, "javascript:alert(1)", TerminalLinkOrigin.Declared),
                unconfirmed);

            run.Check("the list is checked even with the question switched off",
                refusal.Outcome == TerminalLinkOutcome.Refused && none.Count == 0);
            run.Check("the refusal names the address kind",
                refusal.Message != null && refusal.Message.Contains("javascript"));
        }

        private static void Punycode(TestRun run)
        {
            List<string> handed = new List<string>();
            TerminalLinkResult declined = Opener(handed, false).Open(
                new TerminalLink(1, "https://xn--80ak6aa92e.com/", TerminalLinkOrigin.Detected),
                new TerminalLinkOptions());

            run.Check("an encoded host is asked about", declined.Outcome == TerminalLinkOutcome.Cancelled);
            run.Check("an encoded host that is declined never reaches the system", handed.Count == 0);

            TerminalLinkResult accepted = Opener(handed, true).Open(
                new TerminalLink(1, "https://xn--80ak6aa92e.com/", TerminalLinkOrigin.Detected),
                new TerminalLinkOptions());

            run.Check("an encoded host opens once it is agreed to",
                accepted.Outcome == TerminalLinkOutcome.Opened && handed.Count == 1);

            TerminalLinkOptions unconfirmed = new TerminalLinkOptions();
            unconfirmed.ConfirmMisleading = false;

            List<string> straight = new List<string>();
            TerminalLinkResult direct = Opener(straight, false).Open(
                new TerminalLink(1, "https://xn--80ak6aa92e.com/", TerminalLinkOrigin.Detected),
                unconfirmed);

            run.Check("with the question switched off an encoded host opens without one",
                direct.Outcome == TerminalLinkOutcome.Opened && straight.Count == 1);
        }

        private static void NonAscii(TestRun run)
        {
            string host = "https://" + (char)0x0440 + (char)0x0444 + ".com/";
            List<string> handed = new List<string>();

            TerminalLinkResult result = Opener(handed, false).Open(
                new TerminalLink(1, host, TerminalLinkOrigin.Detected),
                new TerminalLinkOptions());

            run.Check("a host written in another script is asked about",
                result.Outcome == TerminalLinkOutcome.Cancelled && handed.Count == 0);
        }

        private static void Mismatch(TestRun run)
        {
            List<TerminalLinkPrompt> asked = new List<TerminalLinkPrompt>();
            List<string> handed = new List<string>();

            TerminalLinkOpener opener = new TerminalLinkOpener(
                delegate(string uri) { handed.Add(uri); return true; },
                delegate(TerminalLinkPrompt prompt) { asked.Add(prompt); return true; });

            TerminalLink declared = new TerminalLink(1, "https://elsewhere.example/pay",
                TerminalLinkOrigin.Declared);
            declared.Text = "https://bank.example";

            TerminalLinkResult result = opener.Open(declared, new TerminalLinkOptions());

            run.Check("a declared link that shows something else is asked about", asked.Count == 1);
            run.Equal("the question names the real target", "https://elsewhere.example/pay",
                asked.Count == 1 ? asked[0].Uri : null);
            run.Check("it opens the target, not what was shown",
                result.Outcome == TerminalLinkOutcome.Opened
                && handed.Count == 1 && handed[0] == "https://elsewhere.example/pay");

            List<TerminalLinkPrompt> quiet = new List<TerminalLinkPrompt>();
            TerminalLinkOpener plain = new TerminalLinkOpener(
                delegate(string uri) { return true; },
                delegate(TerminalLinkPrompt prompt) { quiet.Add(prompt); return true; });

            TerminalLink honest = new TerminalLink(2, "https://example.com/page",
                TerminalLinkOrigin.Declared);
            honest.Text = "https://example.com/page";

            plain.Open(honest, new TerminalLinkOptions());
            run.Check("a declared link that shows its own target is not asked about", quiet.Count == 0);
        }

        private static void Elision(TestRun run)
        {
            string uri = "https://example.com/" + new string('a', 200);
            string shown = TerminalLinkPolicy.Elide(uri, 40);

            run.Equal("a long target is shortened for the question", 40, shown.Length);
            run.Check("the shortened target keeps both ends",
                shown.StartsWith("https://") && shown.EndsWith("aaa") && shown.Contains("..."));
        }

        private static TerminalLinkOpener Opener(List<string> handed, bool agree)
        {
            return new TerminalLinkOpener(
                delegate(string uri) { handed.Add(uri); return true; },
                delegate(TerminalLinkPrompt prompt) { return agree; });
        }
    }
}
