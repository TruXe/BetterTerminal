using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    [DataContract]
    public sealed class PersistedNode
    {
        public const string PaneKind = "pane";
        public const string SplitKind = "split";

        /// <summary>A docked tool - the connection list or the file explorer.</summary>
        public const string ToolKind = "tool";

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "shell")]
        public string ShellName { get; set; }

        [DataMember(Name = "workingDirectory")]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Which tool, when <see cref="Kind"/> is <see cref="ToolKind"/>. Only the identity is kept:
        /// what the tool was showing - an open file, a half-typed address - belongs to the session
        /// that ended, and pretending otherwise would restore a stale view of a folder that may have
        /// changed since.
        /// </summary>
        [DataMember(Name = "tool")]
        public string Tool { get; set; }

        [DataMember(Name = "orientation")]
        public string Orientation { get; set; }

        [DataMember(Name = "firstRatio")]
        public double FirstRatio { get; set; }

        [DataMember(Name = "first")]
        public PersistedNode First { get; set; }

        [DataMember(Name = "second")]
        public PersistedNode Second { get; set; }
    }
}
