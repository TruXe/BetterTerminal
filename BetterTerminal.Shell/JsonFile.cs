using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace BetterTerminal.Shell
{
    /// <summary>
    /// The one way this application reads and writes its own JSON. Every store - the workspace,
    /// the saved connections, a project folder - goes through here so they all share the same
    /// "an unreadable file means no state, never a crash" behaviour.
    /// </summary>
    public static class JsonFile
    {
        public static T Read<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    SkipByteOrderMark(stream);
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    return (T)serializer.ReadObject(stream);
                }
            }
            catch (SerializationException)
            {
                // A file written by an incompatible build, or hand-edited into invalid JSON,
                // starts the shell from defaults instead of failing to start at all.
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public static void Write<T>(string path, T value) where T : class
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using (FileStream stream = File.Create(path))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
            }
        }

        /// <summary>
        /// The reader rejects a byte order mark, and the settings window offers to open these
        /// files, so a hand edit saved from a normal Windows editor would otherwise silently
        /// reset the state it holds.
        /// </summary>
        private static void SkipByteOrderMark(FileStream stream)
        {
            byte[] mark = new byte[3];
            if (stream.Read(mark, 0, 3) == 3 && mark[0] == 0xEF && mark[1] == 0xBB && mark[2] == 0xBF)
            {
                return;
            }

            stream.Position = 0;
        }
    }
}
