using System;
using System.ComponentModel;
using System.Diagnostics;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalLinkOpener
    {
        private const int DisplayLength = 96;

        private readonly Func<string, bool> _hand;
        private readonly Func<TerminalLinkPrompt, bool> _confirm;

        public TerminalLinkOpener()
            : this(null, null)
        {
        }

        public TerminalLinkOpener(Func<string, bool> hand, Func<TerminalLinkPrompt, bool> confirm)
        {
            _hand = hand ?? HandToAssociation;
            _confirm = confirm;
        }

        public TerminalLinkResult Open(TerminalLink link, TerminalLinkOptions options)
        {
            if (link == null || string.IsNullOrEmpty(link.Uri))
            {
                return TerminalLinkResult.Refused(string.Empty);
            }

            if (options == null)
            {
                options = new TerminalLinkOptions();
            }

            if (HasControlCharacter(link.Uri))
            {
                return TerminalLinkResult.Refused("That link was not opened: its address is not a usable one.");
            }

            if (!TerminalLinkPolicy.IsAllowed(link.Uri, options.AllowedSchemes))
            {
                return TerminalLinkResult.Refused(Refusal(TerminalLinkPolicy.SchemeOf(link.Uri)));
            }

            if (options.ConfirmMisleading && TerminalLinkPolicy.IsMisleading(link))
            {
                TerminalLinkPrompt prompt = new TerminalLinkPrompt(
                    link.Uri,
                    TerminalLinkPolicy.Elide(link.Uri, DisplayLength),
                    link.Text);

                if (_confirm == null || !_confirm(prompt))
                {
                    return TerminalLinkResult.Cancelled();
                }
            }

            return _hand(link.Uri)
                ? TerminalLinkResult.Opened()
                : TerminalLinkResult.Failed("Nothing on this computer is set up to open that link.");
        }

        private static string Refusal(string scheme)
        {
            return scheme.Length == 0
                ? "That link was not opened: it names no address kind this terminal opens."
                : "That link was not opened: this terminal does not open " + scheme + " addresses.";
        }

        private static bool HasControlCharacter(string uri)
        {
            foreach (char character in uri)
            {
                if (character < ' ' || character == '\x7f')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HandToAssociation(string uri)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(uri);
                start.UseShellExecute = true;

                using (Process.Start(start))
                {
                    return true;
                }
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
