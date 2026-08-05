using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    /// <summary>
    /// The saved remote connections. Per user, never per project: the address book follows the
    /// person, not the folder they happen to be working in.
    /// </summary>
    [DataContract]
    public sealed class PersistedConnectionBook
    {
        [DataMember(Name = "connections")]
        public List<PersistedConnection> Connections { get; set; }
    }

    /// <summary>
    /// One saved connection. Only the two fields the user typed are kept - there is deliberately
    /// no password, key path or any other secret field to store.
    /// </summary>
    [DataContract]
    public sealed class PersistedConnection
    {
        [DataMember(Name = "userName")]
        public string UserName { get; set; }

        [DataMember(Name = "host")]
        public string Host { get; set; }
    }
}
