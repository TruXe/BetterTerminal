using System;
using System.Text;
using BetterTerminal.Interop;

namespace BetterTerminal.Banner
{
    public static class Program
    {
        private const string ShellSwitch = "--shell";

        public static int Main(string[] args)
        {
            // The switch carries a token rather than a name, so the shell command line that calls
            // this needs no quoting at all. The readable name belongs here.
            string shellName = DisplayName(Read(args, ShellSwitch));

            // A session being piped somewhere gets the facts as plain text: nobody is watching it
            // arrive, and a colour sequence written into a file is just rubbish in the file.
            bool decorated = !Console.IsOutputRedirected && EnableEscapeSequences();

            if (decorated)
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }

            new SessionBanner(decorated, SessionBanner.ConsoleWidth()).Write(shellName);
            return 0;
        }

        private static string DisplayName(string token)
        {
            if (string.Equals(token, "cmd", StringComparison.OrdinalIgnoreCase))
            {
                return "Command Prompt";
            }

            if (string.Equals(token, "powershell", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows PowerShell";
            }

            return string.IsNullOrEmpty(token) ? "Shell" : token;
        }

        private static string Read(string[] args, string name)
        {
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// The classic console host leaves escape-sequence processing off, and the colours here
        /// would arrive as visible gibberish. Returning false keeps the banner plain instead.
        /// </summary>
        private static bool EnableEscapeSequences()
        {
            IntPtr output = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
            int mode;

            if (output == IntPtr.Zero || !NativeMethods.GetConsoleMode(output, out mode))
            {
                return false;
            }

            return (mode & NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0
                || NativeMethods.SetConsoleMode(output,
                    mode | NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
    }
}
