using System;
using System.Collections.Generic;
using System.IO;

namespace BetterTerminal.Wrap
{
    public static class Program
    {
        private const int NoConsole = 2;
        private const int NoScripts = 3;

        public static int Main(string[] args)
        {
            if (Console.IsOutputRedirected || Console.IsInputRedirected)
            {
                Console.Error.WriteLine("This program draws on a console and cannot run with its input or output redirected.");
                return NoConsole;
            }

            string toolsFolder = args.Length > 0
                ? args[0]
                : ScriptCatalog.FindToolsFolder(AppDomain.CurrentDomain.BaseDirectory);

            if (toolsFolder == null || !Directory.Exists(toolsFolder))
            {
                Console.Error.WriteLine("No tools folder found. Pass its path as the first argument.");
                return NoScripts;
            }

            IList<ScriptEntry> scripts = ScriptCatalog.Load(toolsFolder);

            using (TerminalMode terminal = TerminalMode.Acquire())
            {
                if (terminal == null)
                {
                    Console.Error.WriteLine("This console does not support escape sequences.");
                    return NoConsole;
                }

                new WrapApplication(terminal, scripts, toolsFolder).Run();
            }

            return 0;
        }
    }
}
