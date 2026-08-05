namespace BetterTerminal.Wrap
{
    /// <summary>One declared parameter of a script, as the script itself declares it.</summary>
    public sealed class ScriptParameter
    {
        public ScriptParameter(string name, bool required, string defaultValue, string description)
        {
            Name = name;
            Required = required;
            DefaultValue = defaultValue;
            Description = description;
        }

        public string Name { get; private set; }

        public bool Required { get; private set; }

        /// <summary>What the script uses when the parameter is omitted, or null when it has none.</summary>
        public string DefaultValue { get; private set; }

        public string Description { get; private set; }
    }
}
