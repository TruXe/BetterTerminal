using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    /// <summary>
    /// A pane that was living in a window of its own when the application closed. The rectangle is
    /// in physical screen pixels, the same units the window manager reports and the same ones the
    /// docking code drags in, so a restored window lands where it was even on a second monitor of a
    /// different scale.
    /// </summary>
    [DataContract]
    public sealed class PersistedFloating
    {
        [DataMember(Name = "left")]
        public int Left { get; set; }

        [DataMember(Name = "top")]
        public int Top { get; set; }

        [DataMember(Name = "width")]
        public int Width { get; set; }

        [DataMember(Name = "height")]
        public int Height { get; set; }

        /// <summary>What was in it. A leaf today; a subtree costs nothing to allow.</summary>
        [DataMember(Name = "node")]
        public PersistedNode Node { get; set; }
    }
}
