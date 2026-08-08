using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    [DataContract]
    public sealed class PersistedWorkspace
    {
        [DataMember(Name = "backend")]
        public string Backend { get; set; }

        [DataMember(Name = "selectedTab")]
        public int SelectedTab { get; set; }

        [DataMember(Name = "tabs")]
        public List<PersistedTab> Tabs { get; set; }

        /// <summary>Panes that were torn off into windows of their own. Null on an older file.</summary>
        [DataMember(Name = "floating")]
        public List<PersistedFloating> Floating { get; set; }

        [DataMember(Name = "theme")]
        public string Theme { get; set; }

        [DataMember(Name = "scheme")]
        public string Scheme { get; set; }

        [DataMember(Name = "fontFamily")]
        public string FontFamily { get; set; }

        [DataMember(Name = "fontSize")]
        public int FontSize { get; set; }

        [DataMember(Name = "cursorShape")]
        public string CursorShape { get; set; }

        [DataMember(Name = "blinkCursor")]
        public bool BlinkCursor { get; set; }

        [DataMember(Name = "splitUsesActiveProfile")]
        public bool SplitUsesActiveProfile { get; set; }

        [DataMember(Name = "windowLeft")]
        public double WindowLeft { get; set; }

        [DataMember(Name = "windowTop")]
        public double WindowTop { get; set; }

        [DataMember(Name = "windowWidth")]
        public double WindowWidth { get; set; }

        [DataMember(Name = "windowHeight")]
        public double WindowHeight { get; set; }

        [DataMember(Name = "windowMaximized")]
        public bool WindowMaximized { get; set; }
    }
}
