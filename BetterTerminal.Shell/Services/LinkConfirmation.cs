using System;
using System.Windows;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Services
{
    internal static class LinkConfirmation
    {
        private const int ShownTextLength = 80;
        private const string Caption = "Open this link?";

        public static bool Ask(Window owner, TerminalLinkPrompt prompt)
        {
            if (prompt == null)
            {
                return false;
            }

            string question = "This link may not lead where it looks like it leads."
                + Environment.NewLine + Environment.NewLine
                + "It opens: " + prompt.Display;

            if (!string.IsNullOrEmpty(prompt.Text)
                && !string.Equals(prompt.Text, prompt.Uri, StringComparison.OrdinalIgnoreCase))
            {
                question += Environment.NewLine
                    + "It is shown as: " + TerminalLinkPolicy.Elide(prompt.Text, ShownTextLength);
            }

            question += Environment.NewLine + Environment.NewLine + "Open it?";

            MessageBoxResult answer = owner == null
                ? MessageBox.Show(question, Caption, MessageBoxButton.YesNo, MessageBoxImage.Warning)
                : MessageBox.Show(owner, question, Caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            return answer == MessageBoxResult.Yes;
        }
    }
}
