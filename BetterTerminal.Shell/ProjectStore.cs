using System.IO;

namespace BetterTerminal.Shell
{
    /// <summary>
    /// Per-project state, kept in a hidden folder inside the project itself so it travels with
    /// the directory rather than with the machine. Saved connections deliberately do not live
    /// here - they are per-user and belong beside the application settings.
    /// </summary>
    public static class ProjectStore
    {
        public const string FolderName = ".beterm";
        public const string FileName = "project.json";

        public static string FolderFor(string projectDirectory)
        {
            return string.IsNullOrEmpty(projectDirectory)
                ? null
                : Path.Combine(projectDirectory, FolderName);
        }

        public static string FilePathFor(string projectDirectory)
        {
            string folder = FolderFor(projectDirectory);
            return folder == null ? null : Path.Combine(folder, FileName);
        }

        public static PersistedProject Load(string projectDirectory)
        {
            return JsonFile.Read<PersistedProject>(FilePathFor(projectDirectory));
        }

        public static void Save(string projectDirectory, PersistedProject project)
        {
            string path = FilePathFor(projectDirectory);
            if (path == null || project == null)
            {
                return;
            }

            JsonFile.Write(path, project);
            Hide(FolderFor(projectDirectory));
        }

        /// <summary>
        /// The folder is created by the write above; hiding it is a separate, best-effort step so
        /// a directory that refuses the attribute (a network share, a synchronised folder) still
        /// gets its settings saved.
        /// </summary>
        private static void Hide(string folder)
        {
            try
            {
                DirectoryInfo info = new DirectoryInfo(folder);
                if (info.Exists && (info.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                {
                    info.Attributes |= FileAttributes.Hidden;
                }
            }
            catch (IOException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }
        }
    }
}
