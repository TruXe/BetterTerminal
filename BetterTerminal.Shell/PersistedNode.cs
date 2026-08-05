using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    [DataContract]
    public sealed class PersistedNode
    {
        public const string PaneKind = "pane";
        public const string SplitKind = "split";

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "shell")]
        public string ShellName { get; set; }

        [DataMember(Name = "workingDirectory")]
        public string WorkingDirectory { get; set; }

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
