using System.Text;
using System.Windows.Media.Imaging;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// The result of opening one file: exactly one of the three payloads is filled in, and
    /// <see cref="Details"/> is the one line that says what the viewer is showing.
    /// </summary>
    public sealed class OpenedFile
    {
        public OpenedFile(string path, long length)
        {
            Path = path;
            Length = length;
        }

        public string Path { get; private set; }

        public long Length { get; private set; }

        public FileKind Kind { get; private set; }

        public string Text { get; private set; }

        public Encoding Encoding { get; private set; }

        /// <summary>The language its colours come from, or null when there is none for this name.</summary>
        public SyntaxLanguage Language { get; private set; }

        /// <summary>False for a text file that is too large to hold and edit as one string.</summary>
        public bool IsEditable { get; private set; }

        /// <summary>Frozen, so it can be decoded off the interface thread.</summary>
        public BitmapSource Image { get; private set; }

        /// <summary>The readable stand-in for something that is neither text nor a picture.</summary>
        public string Preview { get; private set; }

        public string Details { get; private set; }

        public void SetText(string text, Encoding encoding, bool editable, SyntaxLanguage language)
        {
            Kind = FileKind.Text;
            Text = text;
            Encoding = encoding;
            IsEditable = editable;
            Language = language;
            // Say which of the two reasons left it plain, so an uncoloured file is never a mystery:
            // the size is one thing, an extension nobody has a language for is another.
            string plain = language != null
                ? string.Empty
                : editable
                    ? " - no colours for this kind of file"
                    : string.Empty;

            Details = (language == null ? "text" : language.Name) + ", " + encoding.WebName + ", " + Size(Length) +
                (editable ? plain : " - too large to edit or colour, showing the beginning read-only");
        }

        public void SetImage(BitmapSource image)
        {
            Kind = FileKind.Image;
            Image = image;
            Details = image.PixelWidth + " x " + image.PixelHeight + ", " + Size(Length);
        }

        public void SetBinary(string preview, long shown)
        {
            Kind = FileKind.Binary;
            Preview = preview;
            Details = "not text, " + Size(Length) +
                (shown < Length ? " - showing the first " + Size(shown) : string.Empty);
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("0.#") + " KB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("0.#") + " MB";
        }
    }
}
