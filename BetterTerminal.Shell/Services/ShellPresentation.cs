using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Starts a shell the way this application wants it to look: no start-up banner, the working
    /// directory in the accent colour, and one line naming the product and the project.
    ///
    /// The shell still draws its own prompt - this only tells it what to draw. That is what keeps
    /// every full-screen program working: an editor, a pager or a remote shell takes the screen
    /// over exactly as before, because nothing between the keyboard and the shell has changed.
    /// </summary>
    public static class ShellPresentation
    {
        /// <summary>
        /// The project name reaches the shell through the environment, never on the command line.
        /// A command interpreter expands a variable and then parses what came out, so the value is
        /// also reduced to harmless characters before it is set.
        /// </summary>
        public const string ProjectVariable = "BETERM_PROJECT";

        /// <summary>
        /// The project folder itself, which is what the prompt measures the current directory
        /// against. A path is not a value a shell parses, so it needs no reducing.
        /// </summary>
        public const string WorkspaceVariable = "BETERM_WORKSPACE";

        private const string AccentToken = "Bt.Color.AccentLight";

        /// <summary>
        /// The program that writes the session banner. It is reached by name, from the folder the
        /// command registration puts on the search path, so no path ever has to be quoted into a
        /// shell command line.
        /// </summary>
        private const string BannerCommand = "beterm-banner.exe";

        public static ShellProfile Apply(ShellProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            string executable = Path.GetFileName(profile.Executable);

            if (string.Equals(executable, "cmd.exe", StringComparison.OrdinalIgnoreCase))
            {
                return profile.WithArguments(CommandPromptArguments());
            }

            if (string.Equals(executable, "powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                return profile.WithArguments(PowerShellArguments(profile.Arguments));
            }

            return profile;
        }

        public static void SetProject(string name, string directory)
        {
            Environment.SetEnvironmentVariable(ProjectVariable, Reduce(name),
                EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(WorkspaceVariable, directory ?? string.Empty,
                EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// The interpreter prints its own version banner before it runs anything, and there is no
        /// switch to stop it - so the first thing it is asked to do is clear the screen. PROMPT
        /// understands $E as an escape, which is what colours the machine name.
        ///
        /// Nothing in here is quoted, and nothing carries a path: the banner is found on the
        /// search path, and "where /q" keeps a session that has none from reporting an error.
        /// </summary>
        private static string CommandPromptArguments()
        {
            string colour = AccentSequence();
            string machine = "%COMPUTERNAME%";

            // $G is the PROMPT token for ">". A literal one here would be read as redirection
            // while this command line is parsed, and the prompt would end up in a file called $S.
            string arrow = "$S$G$G$S";

            // $P is the whole path. The interpreter's prompt understands tokens, not expressions,
            // so it cannot measure the path against the project the way the other shell does -
            // showing the real path is the honest answer rather than a prefix that goes stale.
            string prompt = colour == null
                ? machine + "$S$P" + arrow
                : "$E[" + colour + "m" + machine + "$E[0m$S$E[2m$P$E[0m" + arrow;

            return "/k \"prompt " + prompt + " & cls & where /q " + BannerCommand +
                " && " + BannerCommand + " --shell cmd\"";
        }

        /// <summary>
        /// The command is passed encoded: it contains quotes, braces and dollar signs, and every
        /// layer between here and the shell would otherwise get a say in what they mean.
        /// </summary>
        private static string PowerShellArguments(string existing)
        {
            string colour = AccentSequence();
            string machine = "$($env:COMPUTERNAME)";

            // The location is shown against the project: /<project>/<folder>, forward slashes,
            // and the whole path when the shell has walked out of the project entirely - a prompt
            // that hides where it really is would be worse than a long one.
            string location = string.Concat(
                "$(",
                "$r = $env:", WorkspaceVariable, "; ",
                "$h = $ExecutionContext.SessionState.Path.CurrentLocation.Path; ",
                "if ($r -and $h.StartsWith($r, [System.StringComparison]::OrdinalIgnoreCase)) ",
                "{ ('/' + (Split-Path $r -Leaf) + $h.Substring($r.Length)).Replace('\\','/') } ",
                "else { $h.Replace('\\','/') }",
                ")");

            string body = colour == null
                ? "\"" + machine + " " + location + " >> \""
                : "\"$([char]27)[" + colour + "m" + machine + "$([char]27)[0m " +
                  "$([char]27)[2m" + location + "$([char]27)[0m >> \"";

            string script =
                "function prompt { " + body + " }; " +
                "if (Get-Command " + BannerCommand + " -ErrorAction SilentlyContinue) " +
                "{ " + BannerCommand + " --shell powershell }";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return existing + " -NoExit -EncodedCommand " + encoded;
        }

        /// <summary>
        /// The accent as the parameters of a colour sequence, or null when the token is missing -
        /// in which case the prompt stays uncoloured rather than falling back to a literal.
        /// </summary>
        private static string AccentSequence()
        {
            object value = Application.Current == null
                ? null
                : Application.Current.TryFindResource(AccentToken);

            if (!(value is Color))
            {
                return null;
            }

            Color accent = (Color)value;
            return "38;2;" + accent.R + ";" + accent.G + ";" + accent.B;
        }

        private static string Reduce(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            StringBuilder reduced = new StringBuilder(name.Length);

            foreach (char character in name)
            {
                if (char.IsLetterOrDigit(character) || character == ' ' ||
                    character == '.' || character == '-' || character == '_')
                {
                    reduced.Append(character);
                }
            }

            return reduced.ToString();
        }
    }
}
