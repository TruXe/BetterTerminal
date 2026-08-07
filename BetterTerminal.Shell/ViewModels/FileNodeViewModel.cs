using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// One entry of the folder tree. The tree is built off the interface thread and handed over
    /// finished, so nothing here posts back or reads the disk itself.
    /// </summary>
    public class FileNodeViewModel : ObservableObject
    {
        // Icon-font code points, built from their numbers so this file stays plain ASCII.
        private static readonly string FolderGlyph = ((char)0xE8B7).ToString();
        private static readonly string FileGlyph = ((char)0xE7C3).ToString();

        private bool _isExpanded;

        public FileNodeViewModel(string name, string fullPath, bool isDirectory)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            Children = new ObservableCollection<FileNodeViewModel>();
        }

        public string Name { get; private set; }

        public string FullPath { get; private set; }

        public bool IsDirectory { get; private set; }

        public ObservableCollection<FileNodeViewModel> Children { get; private set; }

        public string Glyph
        {
            get { return IsDirectory ? FolderGlyph : FileGlyph; }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { Set(ref _isExpanded, value); }
        }

        public void SetChildren(IEnumerable<FileNodeViewModel> children)
        {
            foreach (FileNodeViewModel child in children)
            {
                Children.Add(child);
            }
        }
    }
}
