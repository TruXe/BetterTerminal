using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BetterTerminal.Wrap
{
    /// <summary>A script plus the values entered for it, ready to be started.</summary>
    public sealed class RunRequest
    {
        public RunRequest(ScriptEntry script, string toolsFolder, IDictionary<string, string> values)
        {
            Script = script;
            ScriptPath = Path.Combine(toolsFolder, script.FileName);
            Values = values;
        }

        public ScriptEntry Script { get; private set; }

        public string ScriptPath { get; private set; }

        public IDictionary<string, string> Values { get; private set; }

        /// <summary>
        /// The parameters as they will be passed. Only values the user actually entered are
        /// included, so an omitted optional parameter keeps the script's own default rather than
        /// this program's transcription of it.
        /// </summary>
        public string BuildArguments()
        {
            StringBuilder arguments = new StringBuilder();

            foreach (ScriptParameter parameter in Script.Parameters)
            {
                string value;
                if (!Values.TryGetValue(parameter.Name, out value) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (arguments.Length > 0)
                {
                    arguments.Append(' ');
                }

                arguments.Append('-').Append(parameter.Name).Append(' ').Append(Quote(value));
            }

            return arguments.ToString();
        }

        public IList<string> MissingRequired()
        {
            List<string> missing = new List<string>();

            foreach (ScriptParameter parameter in Script.Parameters)
            {
                string value;
                if (parameter.Required && (!Values.TryGetValue(parameter.Name, out value) ||
                        string.IsNullOrEmpty(value)))
                {
                    missing.Add(parameter.Name);
                }
            }

            return missing;
        }

        /// <summary>
        /// A value goes onto a command line, so it is always quoted and an embedded quote is
        /// escaped. A trailing backslash before the closing quote would escape that quote, so it
        /// is doubled - the one case where quoting alone is not enough.
        /// </summary>
        private static string Quote(string value)
        {
            StringBuilder quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');

            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    quoted.Append(character);
                    continue;
                }

                if (character == '"')
                {
                    quoted.Append('\\', backslashes + 1);
                }

                backslashes = 0;
                quoted.Append(character);
            }

            quoted.Append('\\', backslashes);
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
