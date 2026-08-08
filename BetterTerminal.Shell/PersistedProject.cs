using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BetterTerminal.Shell
{
    /// <summary>
    /// The settings of one project folder, stored inside that folder. Deliberately holds no
    /// terminal output and no credentials - only what the user typed into the workspace setup.
    /// </summary>
    [DataContract]
    public sealed class PersistedProject
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "shell")]
        public string Shell { get; set; }

        /// <summary>Sent to the first session when the project opens. Empty by default.</summary>
        [DataMember(Name = "startupCommand")]
        public string StartupCommand { get; set; }

        [DataMember(Name = "showSetupOnOpen")]
        public bool ShowSetupOnOpen { get; set; }

        /// <summary>Serve this project's terminal to a browser on this machine.</summary>
        [DataMember(Name = "localServer")]
        public bool LocalServer { get; set; }

        /// <summary>The port the local server listens on; 0 means the default.</summary>
        [DataMember(Name = "localServerPort")]
        public int LocalServerPort { get; set; }

        [DataMember(Name = "commands")]
        public List<PersistedCommand> Commands { get; set; }

        [DataMember(Name = "values")]
        public List<PersistedValue> Values { get; set; }
    }

    /// <summary>A command the user defined for this project; it runs in the focused session.</summary>
    [DataContract]
    public sealed class PersistedCommand
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }
    }

    /// <summary>A named value the user keeps with the project - a path, a host, a build tag.</summary>
    [DataContract]
    public sealed class PersistedValue
    {
        [DataMember(Name = "key")]
        public string Key { get; set; }

        [DataMember(Name = "value")]
        public string Value { get; set; }
    }
}
