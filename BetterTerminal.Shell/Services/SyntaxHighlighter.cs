using System;
using System.Collections.Generic;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Colours one line at a time, carrying what was left open into the next. Line by line rather
    /// than whole-file on purpose: typing changes one line, and only a line that opens or closes a
    /// block forces the ones below it to be read again.
    /// </summary>
    public static class SyntaxHighlighter
    {
        /// <summary>
        /// Fills <paramref name="tokens"/> with the coloured runs of <paramref name="line"/> and
        /// returns what is still open at its end.
        /// </summary>
        public static SyntaxState Read(string line, SyntaxLanguage language, SyntaxState state,
            List<SyntaxToken> tokens)
        {
            tokens.Clear();
            if (language == null || line == null)
            {
                return SyntaxState.Normal;
            }

            switch (language.Family)
            {
                case SyntaxFamily.Markup:
                    return ReadMarkup(line, state, tokens);

                case SyntaxFamily.Json:
                    return ReadJson(line, language, state, tokens);

                default:
                    return ReadGeneric(line, language, state, tokens);
            }
        }

        private static SyntaxState ReadGeneric(string line, SyntaxLanguage language, SyntaxState state,
            List<SyntaxToken> tokens)
        {
            int at = 0;

            if (state == SyntaxState.BlockComment)
            {
                int close = line.IndexOf(language.BlockEnd, StringComparison.Ordinal);
                if (close < 0)
                {
                    Add(tokens, 0, line.Length, TokenKind.Comment);
                    return SyntaxState.BlockComment;
                }

                at = close + language.BlockEnd.Length;
                Add(tokens, 0, at, TokenKind.Comment);
            }

            while (at < line.Length)
            {
                char current = line[at];

                if (char.IsWhiteSpace(current))
                {
                    at++;
                    continue;
                }

                int comment = LineCommentAt(line, at, language);
                if (comment >= 0)
                {
                    Add(tokens, at, line.Length - at, TokenKind.Comment);
                    return SyntaxState.Normal;
                }

                if (language.HasBlockComment && Matches(line, at, language.BlockStart))
                {
                    int close = line.IndexOf(language.BlockEnd, at + language.BlockStart.Length,
                        StringComparison.Ordinal);
                    if (close < 0)
                    {
                        Add(tokens, at, line.Length - at, TokenKind.Comment);
                        return SyntaxState.BlockComment;
                    }

                    int end = close + language.BlockEnd.Length;
                    Add(tokens, at, end - at, TokenKind.Comment);
                    at = end;
                    continue;
                }

                if (current == '"' || current == '\'' || current == '`')
                {
                    at = ReadString(line, at, tokens);
                    continue;
                }

                if (char.IsDigit(current))
                {
                    at = ReadNumber(line, at, tokens);
                    continue;
                }

                // A preprocessor or attribute line reads as a keyword: it is the same kind of
                // instruction to the reader as one.
                if (current == '#' || current == '@' || current == '$')
                {
                    int word = at + 1;
                    while (word < line.Length && IsWordChar(line[word]))
                    {
                        word++;
                    }

                    if (word > at + 1)
                    {
                        Add(tokens, at, word - at, TokenKind.Keyword);
                        at = word;
                        continue;
                    }
                }

                if (IsWordStart(current))
                {
                    int end = at;
                    while (end < line.Length && IsWordChar(line[end]))
                    {
                        end++;
                    }

                    string word = line.Substring(at, end - at);
                    if (language.IsKeyword(word))
                    {
                        Add(tokens, at, end - at, TokenKind.Keyword);
                    }
                    else if (IsCall(line, end))
                    {
                        Add(tokens, at, end - at, TokenKind.Property);
                    }

                    at = end;
                    continue;
                }

                at++;
            }

            return SyntaxState.Normal;
        }

        private static SyntaxState ReadJson(string line, SyntaxLanguage language, SyntaxState state,
            List<SyntaxToken> tokens)
        {
            int at = 0;

            if (state == SyntaxState.BlockComment)
            {
                int close = line.IndexOf("*/", StringComparison.Ordinal);
                if (close < 0)
                {
                    Add(tokens, 0, line.Length, TokenKind.Comment);
                    return SyntaxState.BlockComment;
                }

                at = close + 2;
                Add(tokens, 0, at, TokenKind.Comment);
            }

            while (at < line.Length)
            {
                char current = line[at];

                if (char.IsWhiteSpace(current))
                {
                    at++;
                    continue;
                }

                if (Matches(line, at, "//"))
                {
                    Add(tokens, at, line.Length - at, TokenKind.Comment);
                    return SyntaxState.Normal;
                }

                if (Matches(line, at, "/*"))
                {
                    int close = line.IndexOf("*/", at + 2, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        Add(tokens, at, line.Length - at, TokenKind.Comment);
                        return SyntaxState.BlockComment;
                    }

                    Add(tokens, at, close + 2 - at, TokenKind.Comment);
                    at = close + 2;
                    continue;
                }

                if (current == '"')
                {
                    int start = at;
                    at = ReadString(line, at, tokens);

                    // The name of a member and its value are both quoted; what tells them apart is
                    // the colon after it, and that difference is most of what makes JSON readable.
                    int after = at;
                    while (after < line.Length && char.IsWhiteSpace(line[after]))
                    {
                        after++;
                    }

                    if (after < line.Length && line[after] == ':')
                    {
                        tokens[tokens.Count - 1] = new SyntaxToken(start, at - start, TokenKind.Property);
                    }

                    continue;
                }

                if (char.IsDigit(current) || (current == '-' && at + 1 < line.Length && char.IsDigit(line[at + 1])))
                {
                    at = ReadNumber(line, at, tokens);
                    continue;
                }

                if (IsWordStart(current))
                {
                    int end = at;
                    while (end < line.Length && IsWordChar(line[end]))
                    {
                        end++;
                    }

                    if (language.IsKeyword(line.Substring(at, end - at)))
                    {
                        Add(tokens, at, end - at, TokenKind.Keyword);
                    }

                    at = end;
                    continue;
                }

                if (current == '{' || current == '}' || current == '[' || current == ']' ||
                    current == ':' || current == ',')
                {
                    Add(tokens, at, 1, TokenKind.Operator);
                }

                at++;
            }

            return SyntaxState.Normal;
        }

        private static SyntaxState ReadMarkup(string line, SyntaxState state, List<SyntaxToken> tokens)
        {
            int at = 0;

            if (state == SyntaxState.BlockComment)
            {
                int close = line.IndexOf("-->", StringComparison.Ordinal);
                if (close < 0)
                {
                    Add(tokens, 0, line.Length, TokenKind.Comment);
                    return SyntaxState.BlockComment;
                }

                at = close + 3;
                Add(tokens, 0, at, TokenKind.Comment);
                state = SyntaxState.Normal;
            }

            while (at < line.Length)
            {
                if (state == SyntaxState.InsideTag)
                {
                    char current = line[at];

                    if (char.IsWhiteSpace(current))
                    {
                        at++;
                        continue;
                    }

                    if (current == '>' || Matches(line, at, "/>") || Matches(line, at, "?>"))
                    {
                        int length = current == '>' ? 1 : 2;
                        Add(tokens, at, length, TokenKind.Operator);
                        at += length;
                        state = SyntaxState.Normal;
                        continue;
                    }

                    if (current == '"' || current == '\'')
                    {
                        at = ReadString(line, at, tokens);
                        continue;
                    }

                    if (current == '=')
                    {
                        Add(tokens, at, 1, TokenKind.Operator);
                        at++;
                        continue;
                    }

                    if (IsNameStart(current))
                    {
                        int end = at;
                        while (end < line.Length && IsNameChar(line[end]))
                        {
                            end++;
                        }

                        Add(tokens, at, end - at, TokenKind.Attribute);
                        at = end;
                        continue;
                    }

                    at++;
                    continue;
                }

                if (line[at] != '<')
                {
                    at++;
                    continue;
                }

                if (Matches(line, at, "<!--"))
                {
                    int close = line.IndexOf("-->", at + 4, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        Add(tokens, at, line.Length - at, TokenKind.Comment);
                        return SyntaxState.BlockComment;
                    }

                    Add(tokens, at, close + 3 - at, TokenKind.Comment);
                    at = close + 3;
                    continue;
                }

                int open = at;
                int name = at + 1;
                if (name < line.Length && (line[name] == '/' || line[name] == '?' || line[name] == '!'))
                {
                    name++;
                }

                int tag = name;
                while (tag < line.Length && IsNameChar(line[tag]))
                {
                    tag++;
                }

                Add(tokens, open, name - open, TokenKind.Operator);
                if (tag > name)
                {
                    Add(tokens, name, tag - name, TokenKind.Tag);
                }

                at = tag;
                state = SyntaxState.InsideTag;
            }

            return state == SyntaxState.InsideTag ? SyntaxState.InsideTag : SyntaxState.Normal;
        }

        private static int ReadString(string line, int at, List<SyntaxToken> tokens)
        {
            char quote = line[at];
            int end = at + 1;

            while (end < line.Length)
            {
                if (line[end] == '\\' && end + 1 < line.Length)
                {
                    end += 2;
                    continue;
                }

                if (line[end] == quote)
                {
                    end++;
                    break;
                }

                end++;
            }

            Add(tokens, at, end - at, TokenKind.String);
            return end;
        }

        private static int ReadNumber(string line, int at, List<SyntaxToken> tokens)
        {
            int end = at;
            if (line[end] == '-')
            {
                end++;
            }

            while (end < line.Length &&
                   (char.IsLetterOrDigit(line[end]) || line[end] == '.' || line[end] == '_'))
            {
                end++;
            }

            Add(tokens, at, end - at, TokenKind.Number);
            return end;
        }

        private static int LineCommentAt(string line, int at, SyntaxLanguage language)
        {
            foreach (string marker in language.LineComments)
            {
                if (Matches(line, at, marker) ||
                    (language.IgnoreKeywordCase &&
                     string.Compare(line, at, marker, 0, marker.Length, StringComparison.OrdinalIgnoreCase) == 0))
                {
                    return at;
                }
            }

            return -1;
        }

        /// <summary>A name followed by an opening bracket is being called, which is worth seeing.</summary>
        private static bool IsCall(string line, int end)
        {
            while (end < line.Length && line[end] == ' ')
            {
                end++;
            }

            return end < line.Length && line[end] == '(';
        }

        private static bool Matches(string line, int at, string text)
        {
            return at + text.Length <= line.Length &&
                   string.CompareOrdinal(line, at, text, 0, text.Length) == 0;
        }

        private static void Add(List<SyntaxToken> tokens, int start, int length, TokenKind kind)
        {
            if (length > 0)
            {
                tokens.Add(new SyntaxToken(start, length, kind));
            }
        }

        private static bool IsWordStart(char value)
        {
            return char.IsLetter(value) || value == '_';
        }

        private static bool IsWordChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsNameStart(char value)
        {
            return char.IsLetter(value) || value == '_' || value == ':';
        }

        private static bool IsNameChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '-' ||
                   value == '.' || value == ':';
        }
    }
}
