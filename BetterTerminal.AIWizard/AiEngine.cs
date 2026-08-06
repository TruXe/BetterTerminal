using System.Collections.Generic;

namespace BetterTerminal.AIWizard
{
    /// <summary>The command-line agents the wizard can assemble a command for.</summary>
    public enum AiEngine
    {
        Claude,
        Codex,
        Gemini,
        Antigravity
    }

    /// <summary>
    /// What a single engine is: the name shown to the user, the executable word that starts its
    /// command line, and the key its model list is stored under in the model file. Ported from the
    /// ai.bat launcher, kept as data so a new engine is one entry rather than a new code path.
    /// </summary>
    public sealed class EngineInfo
    {
        private EngineInfo(AiEngine engine, string displayName, string command, string modelKey)
        {
            Engine = engine;
            DisplayName = displayName;
            Command = command;
            ModelKey = modelKey;
        }

        public AiEngine Engine { get; private set; }

        /// <summary>The name the wizard shows in menus.</summary>
        public string DisplayName { get; private set; }

        /// <summary>The word the assembled command line starts with, e.g. "claude" or "agy".</summary>
        public string Command { get; private set; }

        /// <summary>The key this engine's models sit under in the model file.</summary>
        public string ModelKey { get; private set; }

        public static readonly EngineInfo Claude =
            new EngineInfo(AiEngine.Claude, "Claude", "claude", "claude");

        public static readonly EngineInfo Codex =
            new EngineInfo(AiEngine.Codex, "Codex", "codex", "codex");

        public static readonly EngineInfo Gemini =
            new EngineInfo(AiEngine.Gemini, "Gemini", "gemini", "gemini");

        public static readonly EngineInfo Antigravity =
            new EngineInfo(AiEngine.Antigravity, "Antigravity", "agy", "antigravity");

        public static IList<EngineInfo> All
        {
            get { return new List<EngineInfo> { Claude, Codex, Gemini, Antigravity }; }
        }

        public static EngineInfo For(AiEngine engine)
        {
            switch (engine)
            {
                case AiEngine.Codex: return Codex;
                case AiEngine.Gemini: return Gemini;
                case AiEngine.Antigravity: return Antigravity;
                default: return Claude;
            }
        }
    }
}
