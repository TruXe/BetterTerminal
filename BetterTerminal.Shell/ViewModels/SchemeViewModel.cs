using System.Collections.ObjectModel;
using System.Windows.Media;

namespace BetterTerminal.Shell.ViewModels
{
    public class SchemeViewModel
    {
        public string Name { get; set; }

        /// <summary>File name under Themes/Schemes, which is what ThemeService swaps.</summary>
        public string DictionaryName { get; set; }

        public ObservableCollection<Brush> Swatches { get; set; }

        public Brush BackgroundBrush { get; set; }

        public Brush ForegroundBrush { get; set; }

        public Color Background { get; set; }

        public Color Foreground { get; set; }

        public Color Cursor { get; set; }

        public Color Selection { get; set; }

        public string PreviewText { get; set; }
    }
}
