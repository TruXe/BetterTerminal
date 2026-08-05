namespace BetterTerminal.Shell.ViewModels
{
    public sealed class ShortcutViewModel
    {
        public ShortcutViewModel(string action, string keys, string source)
        {
            Action = action;
            Keys = keys;
            Source = source;
        }

        public string Action { get; private set; }

        public string Keys { get; private set; }

        /// <summary>Where the binding lives: the window, the terminal control, or the palette.</summary>
        public string Source { get; private set; }
    }
}
