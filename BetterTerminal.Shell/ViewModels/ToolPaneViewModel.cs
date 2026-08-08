using System.Windows;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// A leaf that holds a tool rather than a session - the connection list, the file explorer. It
    /// is a <see cref="DockLeafViewModel"/> like a pane is, so it splits, tears off into a window
    /// and docks back through exactly the same code, with no branch anywhere that asks which of the
    /// two it is holding.
    ///
    /// The element is handed in already built. A tool panel is expensive to construct and holds live
    /// state - a reachability heart, an open folder, an edited file - and rebuilding it on every
    /// dock would throw that away, which is the same mistake as restarting a session.
    /// </summary>
    public sealed class ToolPaneViewModel : DockLeafViewModel
    {
        /// <summary>Saved in the layout so the tool comes back as itself, not as a session.</summary>
        public const string ConnectionsKind = "connections";

        /// <summary>Saved in the layout; see <see cref="ConnectionsKind"/>.</summary>
        public const string FilesKind = "files";

        private readonly FrameworkElement _content;
        private readonly string _header;

        public ToolPaneViewModel(string kind, string header, FrameworkElement content)
        {
            Kind = kind;
            _header = header;
            _content = content;
        }

        public string Kind { get; private set; }

        public override string HeaderText
        {
            get { return _header; }
        }

        public override FrameworkElement Content
        {
            get { return _content; }
        }

        /// <summary>The icon in the pane header, so a tool reads as a tool at a glance.</summary>
        public string Glyph
        {
            get
            {
                // Built from the code point: this file stays plain ASCII, because the compiler only
                // reads it as UTF-8 when it carries a byte order mark.
                return char.ConvertFromUtf32(Kind == FilesKind ? 0xE8B7 : 0xE968);
            }
        }
    }
}
