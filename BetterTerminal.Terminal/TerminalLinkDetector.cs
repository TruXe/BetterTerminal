using System;
using System.Collections.Generic;

namespace BetterTerminal.Terminal
{
    public static class TerminalLinkDetector
    {
        private const string BareHostPrefix = "www.";
        private const string BareHostScheme = "https://";
        private const string TrailingPunctuation = ".,;:!?'\"";
        private const string AdjoiningCharacters = ".-_/:@%+~#?=&";

        private static readonly string[] Schemes =
        {
            "https://", "http://", "ftp://", "file://", "mailto:"
        };

        public static void Scan(char[] text, int length, bool[] claimed, List<TerminalLinkSpan> results)
        {
            if (text == null || results == null)
            {
                return;
            }

            int index = 0;
            while (index < length)
            {
                int prefix = PrefixLength(text, length, index);
                if (prefix == 0 || !StartsAtBoundary(text, index))
                {
                    index++;
                    continue;
                }

                int end = index;
                while (end < length && IsLinkCharacter(text[end]))
                {
                    end++;
                }

                end = TrimTrailing(text, index, end);

                string uri = Resolve(text, index, end, prefix);
                if (uri == null || IsClaimed(claimed, index, end))
                {
                    index++;
                    continue;
                }

                results.Add(new TerminalLinkSpan(index, end, uri));
                index = end;
            }
        }

        private static int PrefixLength(char[] text, int length, int index)
        {
            foreach (string scheme in Schemes)
            {
                if (Matches(text, length, index, scheme))
                {
                    return scheme.Length;
                }
            }

            return Matches(text, length, index, BareHostPrefix) ? BareHostPrefix.Length : 0;
        }

        private static bool Matches(char[] text, int length, int index, string value)
        {
            if (index + value.Length > length)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (char.ToLowerInvariant(text[index + i]) != value[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StartsAtBoundary(char[] text, int index)
        {
            if (index == 0)
            {
                return true;
            }

            char before = text[index - 1];
            return !char.IsLetterOrDigit(before) && AdjoiningCharacters.IndexOf(before) < 0;
        }

        private static bool IsLinkCharacter(char character)
        {
            return character > ' ' && character != '\x7f';
        }

        private static int TrimTrailing(char[] text, int start, int end)
        {
            while (end > start)
            {
                char last = text[end - 1];

                if (TrailingPunctuation.IndexOf(last) >= 0)
                {
                    end--;
                    continue;
                }

                if (last == ')' && !Balances(text, start, end))
                {
                    end--;
                    continue;
                }

                break;
            }

            return end;
        }

        private static bool Balances(char[] text, int start, int end)
        {
            int opened = 0;
            int closed = 0;

            for (int i = start; i < end; i++)
            {
                if (text[i] == '(')
                {
                    opened++;
                }
                else if (text[i] == ')')
                {
                    closed++;
                }
            }

            return closed <= opened;
        }

        private static string Resolve(char[] text, int start, int end, int prefix)
        {
            if (end - start <= prefix)
            {
                return null;
            }

            string body = new string(text, start, end - start);
            string uri = body.StartsWith(BareHostPrefix, StringComparison.OrdinalIgnoreCase)
                ? BareHostScheme + body
                : body;

            Uri parsed;
            return Uri.TryCreate(uri, UriKind.Absolute, out parsed) ? uri : null;
        }

        private static bool IsClaimed(bool[] claimed, int start, int end)
        {
            if (claimed == null)
            {
                return false;
            }

            for (int i = start; i < end && i < claimed.Length; i++)
            {
                if (claimed[i])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
