using System;
using System.Collections.Generic;

namespace BetterTerminal.Terminal
{
    public static class TerminalLinkPolicy
    {
        private const string PunycodePrefix = "xn--";

        public static string SchemeOf(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return string.Empty;
            }

            int colon = uri.IndexOf(':');
            if (colon <= 0 || !char.IsLetter(uri[0]))
            {
                return string.Empty;
            }

            for (int index = 0; index < colon; index++)
            {
                char character = uri[index];
                if (!char.IsLetterOrDigit(character) && character != '+' && character != '-' && character != '.')
                {
                    return string.Empty;
                }
            }

            return uri.Substring(0, colon).ToLowerInvariant();
        }

        public static bool IsAllowed(string uri, IList<string> allowedSchemes)
        {
            string scheme = SchemeOf(uri);
            if (scheme.Length == 0 || allowedSchemes == null)
            {
                return false;
            }

            foreach (string allowed in allowedSchemes)
            {
                if (string.Equals(allowed, scheme, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsMisleading(TerminalLink link)
        {
            if (link == null || string.IsNullOrEmpty(link.Uri))
            {
                return false;
            }

            string host = HostOf(link.Uri);
            if (HasNonAscii(host) || IsPunycode(host))
            {
                return true;
            }

            return link.Origin == TerminalLinkOrigin.Declared && TextDiffers(link.Text, link.Uri);
        }

        public static string HostOf(string uri)
        {
            int colon = uri.IndexOf(':');
            if (colon < 0)
            {
                return string.Empty;
            }

            int start = colon + 1;
            if (start + 1 < uri.Length && uri[start] == '/' && uri[start + 1] == '/')
            {
                start += 2;
            }

            int end = start;
            while (end < uri.Length && uri[end] != '/' && uri[end] != '?' && uri[end] != '#')
            {
                end++;
            }

            string authority = uri.Substring(start, end - start);

            int credentials = authority.LastIndexOf('@');
            if (credentials >= 0)
            {
                authority = authority.Substring(credentials + 1);
            }

            int bracket = authority.IndexOf(']');
            int port = authority.IndexOf(':', bracket + 1);
            return port >= 0 ? authority.Substring(0, port) : authority;
        }

        public static bool HasNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char character in value)
            {
                if (character > '\x7f')
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsPunycode(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            foreach (string label in host.Split('.'))
            {
                if (label.StartsWith(PunycodePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TextDiffers(string text, string uri)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string shown = text.Trim();
            string target = uri.Trim();

            return !string.Equals(shown, target, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(shown, target.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        public static string Elide(string value, int maximum)
        {
            if (string.IsNullOrEmpty(value) || maximum < 8 || value.Length <= maximum)
            {
                return value;
            }

            int head = (maximum - 3 + 1) / 2;
            int tail = maximum - 3 - head;
            return value.Substring(0, head) + "..." + value.Substring(value.Length - tail);
        }
    }
}
