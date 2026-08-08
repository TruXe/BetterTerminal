using System;
using System.Collections.Generic;
using System.IO;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Services
{
    public static class DroppedPaths
    {
        private static readonly char[] CmdQuoteTriggers = " &()[]{}^=;!'+,`~".ToCharArray();
        private static readonly char[] LineBreaks = { '\r', '\n' };
        private static readonly char[] Separators = { '\\', '/' };

        public static PaneShellKind KindOf(ShellProfile shell, string startupCommand)
        {
            PaneShellKind fromCommand;
            if (TryKindFromName(FirstToken(startupCommand), out fromCommand))
            {
                return fromCommand;
            }

            PaneShellKind fromExecutable;
            if (shell != null && TryKindFromName(shell.Executable, out fromExecutable))
            {
                return fromExecutable;
            }

            return PaneShellKind.Cmd;
        }

        public static string Format(IEnumerable<string> paths, PaneShellKind kind)
        {
            List<string> quoted = new List<string>();

            foreach (string path in paths)
            {
                string cleaned = Clean(path);
                if (cleaned.Length == 0)
                {
                    continue;
                }

                quoted.Add(Quote(kind == PaneShellKind.Wsl ? ToUnixPath(cleaned) : cleaned, kind));
            }

            return string.Join(" ", quoted);
        }

        public static IEnumerable<string> SplitLines(string text)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            foreach (string line in text.Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }

            return lines;
        }

        private static string Clean(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string stripped = path
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\0", string.Empty)
                .Trim();

            return TrimTrailingSeparator(stripped);
        }

        private static string TrimTrailingSeparator(string path)
        {
            int end = path.Length;
            while (end > 3 && Array.IndexOf(Separators, path[end - 1]) >= 0)
            {
                end--;
            }

            return end == path.Length ? path : path.Substring(0, end);
        }

        private static string ToUnixPath(string path)
        {
            if (path.Length < 2 || path[1] != ':' || !char.IsLetter(path[0]))
            {
                return path;
            }

            string tail = path.Substring(2).Replace('\\', '/');
            if (tail.Length == 0 || tail[0] != '/')
            {
                tail = "/" + tail;
            }

            return "/mnt/" + char.ToLowerInvariant(path[0]) + tail;
        }

        private static string Quote(string path, PaneShellKind kind)
        {
            switch (kind)
            {
                case PaneShellKind.PowerShell:
                    return "'" + path.Replace("'", "''") + "'";

                case PaneShellKind.Wsl:
                case PaneShellKind.Ssh:
                    return "'" + path.Replace("'", "'\\''") + "'";

                default:
                    return path.IndexOfAny(CmdQuoteTriggers) >= 0 ? "\"" + path + "\"" : path;
            }
        }

        private static string FirstToken(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine))
            {
                return null;
            }

            string trimmed = commandLine.Trim();
            int space = trimmed.IndexOf(' ');
            return space < 0 ? trimmed : trimmed.Substring(0, space);
        }

        private static bool TryKindFromName(string executable, out PaneShellKind kind)
        {
            kind = PaneShellKind.Cmd;
            if (string.IsNullOrEmpty(executable))
            {
                return false;
            }

            string name;
            try
            {
                name = Path.GetFileNameWithoutExtension(executable);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            switch (name.ToLowerInvariant())
            {
                case "cmd":
                    kind = PaneShellKind.Cmd;
                    return true;

                case "powershell":
                case "pwsh":
                    kind = PaneShellKind.PowerShell;
                    return true;

                case "wsl":
                    kind = PaneShellKind.Wsl;
                    return true;

                case "ssh":
                    kind = PaneShellKind.Ssh;
                    return true;

                default:
                    return false;
            }
        }
    }
}
