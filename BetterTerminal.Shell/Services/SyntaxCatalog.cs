using System;
using System.Collections.Generic;
using System.IO;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Which language a file name is written in. A name that is not listed gets no colours rather
    /// than the wrong ones - guessing a grammar from the bytes is how a log ends up striped.
    /// </summary>
    public static class SyntaxCatalog
    {
        private const string CSharpKeywords =
            "abstract as async await base bool break byte case catch char checked class const continue decimal " +
            "default delegate do double else enum event explicit extern false finally fixed float for foreach get " +
            "goto if implicit in int interface internal is lock long namespace new null object operator out " +
            "override params partial private protected public readonly ref return sbyte sealed set short sizeof " +
            "stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort " +
            "using var virtual void volatile where while yield";

        private const string CppKeywords =
            "auto bool break case catch char class const constexpr continue default delete do double else enum " +
            "explicit export extern false float for friend goto if inline int long mutable namespace new noexcept " +
            "nullptr operator private protected public register return short signed sizeof static struct switch " +
            "template this throw true try typedef typename union unsigned using virtual void volatile wchar_t while";

        private const string JavaScriptKeywords =
            "as async await break case catch class const continue debugger default delete do else export extends " +
            "false finally for from function get if implements import in instanceof interface let new null of " +
            "return set static super switch this throw true try typeof undefined var void while yield";

        private const string PythonKeywords =
            "and as assert async await break class continue def del elif else except False finally for from global " +
            "if import in is lambda None nonlocal not or pass raise return True try while with yield";

        private const string PowerShellKeywords =
            "begin break catch class continue data define do dynamicparam else elseif end enum exit filter finally " +
            "for foreach from function if in param process return switch throw trap try until using var while";

        private const string BatchKeywords =
            "call cd copy del dir do echo else endlocal errorlevel exist exit for goto if in md move not off pause " +
            "popd pushd rd rem ren set setlocal shift start goto";

        private const string SqlKeywords =
            "add all alter and as asc begin between by case cast column commit create cross delete desc distinct " +
            "drop else end exists from full group having if in index inner insert into is join key left like limit " +
            "not null on or order outer primary references right rollback select set table then top union unique " +
            "update values view when where";

        private const string JavaKeywords =
            "abstract assert boolean break byte case catch char class const continue default do double else enum " +
            "extends false final finally float for goto if implements import instanceof int interface long native " +
            "new null package private protected public return short static strictfp super switch synchronized this " +
            "throw throws transient true try void volatile while";

        private const string GoKeywords =
            "break case chan const continue default defer else fallthrough for func go goto if import interface " +
            "map package range return select struct switch type var true false nil";

        private const string RustKeywords =
            "as async await break const continue crate dyn else enum extern false fn for if impl in let loop match " +
            "mod move mut pub ref return self Self static struct super trait true type unsafe use where while";

        private const string CssKeywords =
            "important media import charset keyframes supports from to and not only";

        private const string ShellKeywords =
            "case do done elif else esac fi for function if in return select then until while time coproc";

        private static readonly Dictionary<string, SyntaxLanguage> Languages = Build();

        /// <summary>The language for this file name, or null when there is none.</summary>
        public static SyntaxLanguage For(string path)
        {
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                // A few names carry their language without one.
                string name = Path.GetFileName(path);
                extension = string.Equals(name, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Makefile", StringComparison.OrdinalIgnoreCase)
                    ? ".sh"
                    : null;
            }

            if (extension == null)
            {
                return null;
            }

            SyntaxLanguage language;
            return Languages.TryGetValue(extension.ToLowerInvariant(), out language) ? language : null;
        }

        private static Dictionary<string, SyntaxLanguage> Build()
        {
            Dictionary<string, SyntaxLanguage> map =
                new Dictionary<string, SyntaxLanguage>(StringComparer.OrdinalIgnoreCase);

            SyntaxLanguage csharp = Slashes("C#", CSharpKeywords);
            Add(map, csharp, ".cs", ".csx");

            SyntaxLanguage cpp = Slashes("C++", CppKeywords);
            Add(map, cpp, ".c", ".h", ".cpp", ".hpp", ".cc", ".cxx", ".hxx", ".ino");

            SyntaxLanguage javascript = Slashes("JavaScript", JavaScriptKeywords);
            Add(map, javascript, ".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs");

            Add(map, Slashes("Java", JavaKeywords), ".java");
            Add(map, Slashes("Go", GoKeywords), ".go");
            Add(map, Slashes("Rust", RustKeywords), ".rs");
            Add(map, Slashes("PHP", JavaScriptKeywords), ".php");
            Add(map, Slashes("CSS", CssKeywords), ".css", ".scss", ".less");

            SyntaxLanguage sql = new SyntaxLanguage("SQL", SyntaxFamily.Generic,
                new[] { "--" }, "/*", "*/", SqlKeywords);
            sql.IgnoreKeywordCase = true;
            Add(map, sql, ".sql");

            SyntaxLanguage python = new SyntaxLanguage("Python", SyntaxFamily.Generic,
                new[] { "#" }, null, null, PythonKeywords);
            Add(map, python, ".py", ".pyw");

            SyntaxLanguage powershell = new SyntaxLanguage("PowerShell", SyntaxFamily.Generic,
                new[] { "#" }, "<#", "#>", PowerShellKeywords);
            powershell.IgnoreKeywordCase = true;
            Add(map, powershell, ".ps1", ".psm1", ".psd1");

            SyntaxLanguage shell = new SyntaxLanguage("Shell", SyntaxFamily.Generic,
                new[] { "#" }, null, null, ShellKeywords);
            Add(map, shell, ".sh", ".bash", ".zsh", ".yml", ".yaml", ".toml", ".ini", ".cfg", ".conf",
                ".gitignore", ".gitattributes", ".editorconfig", ".env", ".properties");

            SyntaxLanguage batch = new SyntaxLanguage("Batch", SyntaxFamily.Generic,
                new[] { "::", "rem " }, null, null, BatchKeywords);
            batch.IgnoreKeywordCase = true;
            Add(map, batch, ".bat", ".cmd");

            SyntaxLanguage json = new SyntaxLanguage("JSON", SyntaxFamily.Json,
                new[] { "//" }, "/*", "*/", "true false null");
            Add(map, json, ".json", ".jsonc", ".json5", ".webmanifest");

            SyntaxLanguage markup = new SyntaxLanguage("Markup", SyntaxFamily.Markup,
                null, "<!--", "-->", null);
            Add(map, markup, ".xml", ".xaml", ".html", ".htm", ".xhtml", ".svg", ".config", ".csproj",
                ".vcxproj", ".props", ".targets", ".resx", ".plist", ".vsixmanifest", ".axaml");

            return map;
        }

        private static SyntaxLanguage Slashes(string name, string keywords)
        {
            return new SyntaxLanguage(name, SyntaxFamily.Generic, new[] { "//" }, "/*", "*/", keywords);
        }

        private static void Add(Dictionary<string, SyntaxLanguage> map, SyntaxLanguage language,
            params string[] extensions)
        {
            foreach (string extension in extensions)
            {
                map[extension] = language;
            }
        }
    }
}
