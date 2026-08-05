using System;
using System.IO;

namespace BetterTerminal.Shell
{
    /// <summary>
    /// The application-wide state: appearance, window placement and the tab layout. Everything
    /// here lives under the roaming application data folder and is shared by every workspace;
    /// per-project state belongs in <see cref="ProjectStore"/> instead.
    /// </summary>
    public static class SessionStore
    {
        public static string SettingsFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BetterTerminal");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(SettingsFolder, "workspace.json"); }
        }

        public static PersistedWorkspace Load()
        {
            return JsonFile.Read<PersistedWorkspace>(FilePath);
        }

        public static void Save(PersistedWorkspace workspace)
        {
            JsonFile.Write(FilePath, workspace);
        }
    }
}
