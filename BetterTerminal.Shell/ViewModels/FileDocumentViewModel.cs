using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BetterTerminal.Shell.Services;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// One open file: its text, whether it differs from what is on disk, and the encoding it was
    /// read with so writing it back does not change the bytes around the text.
    /// </summary>
    public class FileDocumentViewModel : ObservableObject
    {
        private string _text;
        private bool _isDirty;
        private bool _loading;
        private bool _isEditable;

        public FileDocumentViewModel(string fullPath)
        {
            FullPath = fullPath;
            Name = Path.GetFileName(fullPath);
        }

        public string FullPath { get; private set; }

        public string Name { get; private set; }

        public Encoding Encoding { get; private set; }

        public FileKind Kind { get; private set; }

        /// <summary>The picture, for a file that turned out to be one.</summary>
        public BitmapSource Image { get; private set; }

        /// <summary>The dump shown for a file that is neither text nor a picture.</summary>
        public string Preview { get; private set; }

        /// <summary>One line saying what is on screen: the encoding, the size, the dimensions.</summary>
        public string Details { get; private set; }

        /// <summary>The language its colours come from, or null when it has none.</summary>
        public SyntaxLanguage Language { get; private set; }

        public bool IsText
        {
            get { return Kind == FileKind.Text; }
        }

        /// <summary>Text in a language the viewer knows, and small enough to colour.</summary>
        public bool IsCode
        {
            get { return Kind == FileKind.Text && Language != null; }
        }

        /// <summary>Text with no colours: an unknown language, or one too large to colour.</summary>
        public bool IsPlainText
        {
            get { return Kind == FileKind.Text && Language == null; }
        }

        public bool IsImage
        {
            get { return Kind == FileKind.Image; }
        }

        public bool IsBinary
        {
            get { return Kind == FileKind.Binary; }
        }

        /// <summary>A picture and a file too large to hold are shown, never edited.</summary>
        public bool IsReadOnly
        {
            get { return !_isEditable; }
        }

        public string Text
        {
            get { return _text; }

            set
            {
                if (Set(ref _text, value) && !_loading && _isEditable)
                {
                    IsDirty = true;
                }
            }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            set { Set(ref _isDirty, value); }
        }

        public ICommand CloseCommand { get; set; }

        /// <summary>What is on disk: it arrives clean, not as an edit.</summary>
        public void Show(OpenedFile opened)
        {
            Kind = opened.Kind;
            Language = opened.Language;
            Encoding = opened.Encoding;
            Image = opened.Image;
            Preview = opened.Preview;
            Details = opened.Details;
            _isEditable = opened.Kind == FileKind.Text && opened.IsEditable;

            _loading = true;
            Text = opened.Text;
            _loading = false;
            IsDirty = false;

            Raise("Kind");
            Raise("Image");
            Raise("Preview");
            Raise("Details");
            Raise("Language");
            Raise("IsText");
            Raise("IsCode");
            Raise("IsPlainText");
            Raise("IsImage");
            Raise("IsBinary");
            Raise("IsReadOnly");
        }

        public void MarkSaved()
        {
            IsDirty = false;
        }
    }
}
