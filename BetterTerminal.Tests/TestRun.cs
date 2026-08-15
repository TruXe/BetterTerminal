using System;
using System.Collections.Generic;

namespace BetterTerminal.Tests
{
    public sealed class TestRun
    {
        private readonly List<string> _failures = new List<string>();

        public int Passed { get; private set; }

        public int Failed
        {
            get { return _failures.Count; }
        }

        public void Check(string name, bool condition)
        {
            if (condition)
            {
                Passed++;
                Console.WriteLine("  ok    " + name);
                return;
            }

            _failures.Add(name);
            Console.WriteLine("  FAIL  " + name);
        }

        public void Equal(string name, string expected, string actual)
        {
            Check(name + " [" + Show(expected) + " vs " + Show(actual) + "]",
                string.Equals(expected, actual, StringComparison.Ordinal));
        }

        public void Equal(string name, int expected, int actual)
        {
            Check(name + " [" + expected + " vs " + actual + "]", expected == actual);
        }

        public void Section(string title)
        {
            Console.WriteLine(title);
        }

        public int Report()
        {
            Console.WriteLine();
            Console.WriteLine(Failed == 0
                ? "RESULT: " + Passed + " check(s) passed"
                : "RESULT: " + Failed + " of " + (Passed + Failed) + " check(s) failed");

            foreach (string failure in _failures)
            {
                Console.WriteLine("  " + failure);
            }

            return Failed == 0 ? 0 : 1;
        }

        private static string Show(string value)
        {
            return value == null ? "<none>" : value;
        }
    }
}
