using System.Collections.Generic;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// Scrollback for one run. Written by the two reader threads of the child and read by the UI
    /// thread, so every member takes the lock; Version lets the UI redraw only when something
    /// arrived instead of on a timer.
    /// </summary>
    public sealed class OutputLog
    {
        public const int MaximumLines = 5000;

        private readonly object _sync = new object();
        private readonly List<string> _lines = new List<string>();

        private int _version;

        public int Version
        {
            get
            {
                lock (_sync)
                {
                    return _version;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _lines.Count;
                }
            }
        }

        public void Append(string line)
        {
            lock (_sync)
            {
                _lines.Add(line ?? string.Empty);
                if (_lines.Count > MaximumLines)
                {
                    _lines.RemoveRange(0, _lines.Count - MaximumLines);
                }

                _version++;
            }
        }

        /// <summary>Copies a window of lines out under the lock; the caller renders from the copy.</summary>
        public IList<string> Window(int first, int count)
        {
            lock (_sync)
            {
                List<string> window = new List<string>(count);

                for (int index = first; index < first + count && index < _lines.Count; index++)
                {
                    if (index >= 0)
                    {
                        window.Add(_lines[index]);
                    }
                }

                return window;
            }
        }
    }
}
