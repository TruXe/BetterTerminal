using System.Collections.Generic;
using System.IO;

namespace BetterTerminal.Shell
{
    /// <summary>Loads and saves the address book beside the application settings.</summary>
    public static class ConnectionStore
    {
        public static string FilePath
        {
            get { return Path.Combine(SessionStore.SettingsFolder, "connections.json"); }
        }

        public static List<PersistedConnection> Load()
        {
            PersistedConnectionBook book = JsonFile.Read<PersistedConnectionBook>(FilePath);
            return book == null || book.Connections == null
                ? new List<PersistedConnection>()
                : book.Connections;
        }

        public static void Save(List<PersistedConnection> connections)
        {
            PersistedConnectionBook book = new PersistedConnectionBook();
            book.Connections = connections ?? new List<PersistedConnection>();
            JsonFile.Write(FilePath, book);
        }
    }
}
