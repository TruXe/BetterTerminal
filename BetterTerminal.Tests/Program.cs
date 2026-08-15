using System;

namespace BetterTerminal.Tests
{
    public static class Program
    {
        public static int Main()
        {
            TestRun run = new TestRun();

            HyperlinkTests.Run(run);
            DetectionTests.Run(run);
            HitTestTests.Run(run);
            OpeningTests.Run(run);

            Console.Out.Flush();
            return run.Report();
        }
    }
}
