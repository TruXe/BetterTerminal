using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// The files view: the folder tree on the left and the open files on the right. It reads and
    /// writes nothing itself - it raises the two events below and the workspace does that work.
    /// </summary>
    public class FileExplorerViewModel : ObservableObject
    {
        private FileNodeViewModel _selectedNode;
        private FileDocumentViewModel _selectedDocument;
        private string _rootPath;
        private string _message;

        public FileExplorerViewModel()
        {
            Roots = new ObservableCollection<FileNodeViewModel>();
            Documents = new ObservableCollection<FileDocumentViewModel>();
            SaveCommand = new ShellCommand(RequestSave);
            _message = "Pick a file on the left to open it.";

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                FileNodeViewModel root = new FileNodeViewModel("project", @"C:\project", true);
                root.SetChildren(new[]
                {
                    new FileNodeViewModel("src", @"C:\project\src", true),
                    new FileNodeViewModel("README.md", @"C:\project\README.md", false)
                });
                root.IsExpanded = true;
                Roots.Add(root);
            }
        }

        /// <summary>A file was picked and should be read.</summary>
        public event EventHandler OpenRequested;

        /// <summary>The open file should be written back to disk.</summary>
        public event EventHandler SaveRequested;

        public ObservableCollection<FileNodeViewModel> Roots { get; private set; }

        public ObservableCollection<FileDocumentViewModel> Documents { get; private set; }

        public FileNodeViewModel SelectedNode
        {
            get { return _selectedNode; }
            set { Set(ref _selectedNode, value); }
        }

        public FileDocumentViewModel SelectedDocument
        {
            get { return _selectedDocument; }
            set { Set(ref _selectedDocument, value); }
        }

        public string RootPath
        {
            get { return _rootPath; }
            set { Set(ref _rootPath, value); }
        }

        /// <summary>What the strip along the bottom says: never a silent failure.</summary>
        public string Message
        {
            get { return _message; }
            set { Set(ref _message, value); }
        }

        public ICommand SaveCommand { get; private set; }

        /// <summary>Picking a file in the tree is what opens it; picking a folder only selects.</summary>
        public void Select(FileNodeViewModel node)
        {
            SelectedNode = node;

            if (node != null && !node.IsDirectory)
            {
                EventHandler handler = OpenRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public void SetRoot(FileNodeViewModel root)
        {
            Roots.Clear();
            if (root != null)
            {
                Roots.Add(root);
            }
        }

        public FileDocumentViewModel Find(string fullPath)
        {
            foreach (FileDocumentViewModel document in Documents)
            {
                if (string.Equals(document.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }

            return null;
        }

        public void Close(FileDocumentViewModel document)
        {
            Documents.Remove(document);
            if (SelectedDocument == document)
            {
                SelectedDocument = Documents.Count > 0 ? Documents[Documents.Count - 1] : null;
            }
        }

        private void RequestSave()
        {
            EventHandler handler = SaveRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
