using System.Text;

namespace BetterTerminal.AIWizard
{
    /// <summary>
    /// Cleans a value typed by the user before it is placed on the command line the wizard runs.
    /// The command is handed to the command interpreter, which expands and re-parses what it is
    /// given, so the characters it treats as operators are removed here rather than trusted to
    /// quoting - the same allow-list approach the ai.bat launcher takes with model ids.
    /// </summary>
    public static class TextSanitizer
    {
        /// <summary>
        /// Removes characters the interpreter would act on - redirection, piping, chaining, quoting
        /// and its escape and variable markers - along with any control character. Everything a
        /// path, an id, a flag list or a prompt legitimately needs is kept.
        /// </summary>
        public static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder cleaned = new StringBuilder(value.Length);

            foreach (char character in value.Trim())
            {
                if (character < ' ')
                {
                    continue;
                }

                switch (character)
                {
                    case '&':
                    case '|':
                    case '<':
                    case '>':
                    case '^':
                    case '"':
                    case '`':
                    case '%':
                    case '!':
                        continue;
                    default:
                        cleaned.Append(character);
                        break;
                }
            }

            return cleaned.ToString();
        }

        /// <summary>Wraps a cleaned value in quotes when it carries a space, and never otherwise.</summary>
        public static string Quote(string cleaned)
        {
            if (string.IsNullOrEmpty(cleaned))
            {
                return string.Empty;
            }

            return cleaned.IndexOf(' ') >= 0 ? "\"" + cleaned + "\"" : cleaned;
        }
    }
}
