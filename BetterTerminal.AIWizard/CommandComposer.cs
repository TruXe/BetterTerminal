using System.Collections.Generic;
using System.Text;

namespace BetterTerminal.AIWizard
{
    /// <summary>
    /// Turns the fragments a run through the wizard produced into the final command line: the
    /// engine's word followed by every non-empty fragment, in the order the steps were answered.
    /// It joins already-formed pieces and adds nothing of its own - a fragment that needed a value
    /// or a flag already carries it.
    /// </summary>
    public sealed class CommandComposer
    {
        private readonly EngineInfo _engine;
        private readonly List<string> _fragments = new List<string>();

        public CommandComposer(EngineInfo engine)
        {
            _engine = engine;
        }

        public EngineInfo Engine
        {
            get { return _engine; }
        }

        /// <summary>Records a fragment; a null or blank one is ignored.</summary>
        public void Add(string fragment)
        {
            if (!string.IsNullOrEmpty(fragment) && fragment.Trim().Length > 0)
            {
                _fragments.Add(fragment.Trim());
            }
        }

        /// <summary>Fills a choice's "{0}" with a value the user typed, cleaned and quoted.</summary>
        public static string FillValue(string fragment, string value)
        {
            string cleaned = TextSanitizer.Clean(value);
            if (cleaned.Length == 0)
            {
                // A prompted option left blank contributes nothing, matching a skip.
                return string.Empty;
            }

            string quoted = TextSanitizer.Quote(cleaned);
            return fragment.Contains("{0}") ? fragment.Replace("{0}", quoted) : fragment + " " + quoted;
        }

        public string Build()
        {
            StringBuilder command = new StringBuilder(_engine.Command);

            foreach (string fragment in _fragments)
            {
                command.Append(' ').Append(fragment);
            }

            return command.ToString();
        }
    }
}
