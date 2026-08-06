using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BetterTerminal.AIWizard
{
    /// <summary>One model as the file records it: an id to pass to the agent and a short label.</summary>
    [DataContract]
    public sealed class ModelEntry
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "desc")]
        public string Description { get; set; }

        public ModelEntry()
        {
        }

        public ModelEntry(string id, string description)
        {
            Id = id;
            Description = description;
        }
    }

    /// <summary>The model file's shape: a list per engine, keyed the way the launcher keys them.</summary>
    [DataContract]
    public sealed class ModelFile
    {
        [DataMember(Name = "claude")]
        public List<ModelEntry> Claude { get; set; }

        [DataMember(Name = "codex")]
        public List<ModelEntry> Codex { get; set; }

        [DataMember(Name = "gemini")]
        public List<ModelEntry> Gemini { get; set; }

        [DataMember(Name = "antigravity")]
        public List<ModelEntry> Antigravity { get; set; }
    }

    /// <summary>
    /// The suggested models the model step offers. It reads the same ai-models.json the launcher
    /// keeps - written under the per-user application data folder - and falls back to a small
    /// built-in list when the file is missing or cannot be read. The file is only a convenience:
    /// the model step always lets the user type an id of their own, so a stale or absent file never
    /// blocks a run.
    ///
    /// The launcher's online refresh of this list is not ported. The application makes a single kind
    /// of network call and this program makes none; the file is read if present and seeded with a
    /// built-in default otherwise.
    /// </summary>
    public sealed class ModelCatalog
    {
        private readonly ModelFile _models;

        private ModelCatalog(ModelFile models)
        {
            _models = models ?? Defaults();
        }

        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BetterTerminal",
                    "ai-models.json");
            }
        }

        /// <summary>
        /// Reads the file if it is there, seeds it with the built-in list if it is not, and returns
        /// the built-in list unchanged if either step fails - reading a model list must never be a
        /// reason a command cannot be assembled.
        /// </summary>
        public static ModelCatalog Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (FileStream stream = File.OpenRead(FilePath))
                    {
                        DataContractJsonSerializer serializer =
                            new DataContractJsonSerializer(typeof(ModelFile));
                        ModelFile file = serializer.ReadObject(stream) as ModelFile;
                        if (file != null)
                        {
                            return new ModelCatalog(file);
                        }
                    }
                }
                else
                {
                    WriteDefaults();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (SerializationException)
            {
            }

            return new ModelCatalog(Defaults());
        }

        public IList<ModelEntry> For(AiEngine engine)
        {
            switch (engine)
            {
                case AiEngine.Codex: return _models.Codex ?? new List<ModelEntry>();
                case AiEngine.Gemini: return _models.Gemini ?? new List<ModelEntry>();
                case AiEngine.Antigravity: return _models.Antigravity ?? new List<ModelEntry>();
                default: return _models.Claude ?? new List<ModelEntry>();
            }
        }

        private static void WriteDefaults()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ModelFile));
                using (MemoryStream buffer = new MemoryStream())
                {
                    serializer.WriteObject(buffer, Defaults());
                    File.WriteAllBytes(FilePath, buffer.ToArray());
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// A small built-in list. These are only menu suggestions - the ids age, so the model step
        /// always offers a custom entry - but they save typing for the common choices.
        /// </summary>
        private static ModelFile Defaults()
        {
            return new ModelFile
            {
                Claude = new List<ModelEntry>
                {
                    new ModelEntry("claude-opus-5", "Opus 5 - most capable"),
                    new ModelEntry("claude-sonnet-5", "Sonnet 5 - balanced"),
                    new ModelEntry("claude-haiku-4-5-20251001", "Haiku 4.5 - fastest"),
                    new ModelEntry("claude-fable-5", "Fable 5")
                },
                Codex = new List<ModelEntry>
                {
                    new ModelEntry("gpt-5-codex", "Codex coding model"),
                    new ModelEntry("gpt-5", "General frontier model")
                },
                Gemini = new List<ModelEntry>
                {
                    new ModelEntry("gemini-2.5-pro", "Pro - most capable"),
                    new ModelEntry("gemini-2.5-flash", "Flash - fastest")
                },
                Antigravity = new List<ModelEntry>()
            };
        }
    }
}
