using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    [DataContract]
    public sealed class PersistedTab
    {
        [DataMember(Name = "header")]
        public string Header { get; set; }

        [DataMember(Name = "root")]
        public PersistedNode Root { get; set; }
    }
}
