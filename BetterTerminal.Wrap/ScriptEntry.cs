using System.Collections.Generic;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// One script this program can start. The file itself is never read, written or rewritten -
    /// this is only what has to be known to invoke it and to show the result.
    /// </summary>
    public sealed class ScriptEntry
    {
        public ScriptEntry(string fileName, string summary, bool takesOverTerminal,
            IList<ScriptParameter> parameters)
        {
            FileName = fileName;
            Summary = summary;
            TakesOverTerminal = takesOverTerminal;
            Parameters = parameters;
        }

        /// <summary>File name inside the tools folder, extension included.</summary>
        public string FileName { get; private set; }

        public string Summary { get; private set; }

        /// <summary>
        /// True when the script must own the console instead of having its output piped here.
        /// Two kinds of script need it: one that draws its own full-screen interface or prompts
        /// for input - a remote shell, an editor, a pager - and one that starts a child which
        /// inherits the standard handles and would write into the pipe instead of its own console.
        /// Both are run with this program's screen put away and restored afterwards.
        /// </summary>
        public bool TakesOverTerminal { get; private set; }

        public IList<ScriptParameter> Parameters { get; private set; }
    }
}
