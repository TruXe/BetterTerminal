using System;
using System.Collections.Generic;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// How one family of languages is written down, as data rather than as code: where a comment
    /// starts, what quotes a string, and which words are keywords. Two families need real parsing
    /// instead - see <see cref="SyntaxFamily"/>.
    /// </summary>
    public sealed class SyntaxLanguage
    {
        private readonly HashSet<string> _keywords;

        public SyntaxLanguage(string name, SyntaxFamily family, string[] lineComments,
            string blockStart, string blockEnd, string keywords)
        {
            Name = name;
            Family = family;
            LineComments = lineComments ?? new string[0];
            BlockStart = blockStart;
            BlockEnd = blockEnd;

            _keywords = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(keywords))
            {
                foreach (string word in keywords.Split(' '))
                {
                    if (word.Length > 0)
                    {
                        _keywords.Add(word);
                    }
                }
            }
        }

        public string Name { get; private set; }

        public SyntaxFamily Family { get; private set; }

        public string[] LineComments { get; private set; }

        public string BlockStart { get; private set; }

        public string BlockEnd { get; private set; }

        public bool HasBlockComment
        {
            get { return !string.IsNullOrEmpty(BlockStart) && !string.IsNullOrEmpty(BlockEnd); }
        }

        /// <summary>Case matters everywhere except the shells, which is what the flag is for.</summary>
        public bool IgnoreKeywordCase { get; set; }

        public bool IsKeyword(string word)
        {
            if (_keywords.Contains(word))
            {
                return true;
            }

            return IgnoreKeywordCase && _keywords.Contains(word.ToLowerInvariant());
        }
    }
}
