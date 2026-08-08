using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Listing, reading and writing the files of the open folder. Every call does its work on a
    /// pool thread and posts the result back to the caller's dispatcher, so a slow disk or a
    /// network share never freezes the window.
    /// </summary>
    public static class WorkspaceFiles
    {
        /// <summary>
        /// Above this a text file is shown but not edited: the whole text is held in memory and
        /// edited as one string, and a larger file would stall the window for seconds per keystroke.
        /// </summary>
        public const long MaximumEditableBytes = 2L * 1024 * 1024;

        /// <summary>A picture is decoded whole or not at all, so it gets a limit of its own.</summary>
        public const long MaximumImageBytes = 64L * 1024 * 1024;

        /// <summary>Above this a text file is shown without colours, because colouring it is slow.</summary>
        /// <summary>
        /// Kept as the name the rest of the code knows, and tied to the editable limit so the two
        /// cannot drift apart again: a file that opens for editing opens with its colours.
        /// </summary>
        public const long MaximumColouredBytes = MaximumEditableBytes;

        /// <summary>
        /// How much of a file is read to decide what it is, and how much of one that turns out to
        /// be neither text nor a picture is shown. A dump of the beginning answers "what is this",
        /// which is the only question left at that point.
        /// </summary>
        public const int PreviewBytes = 64 * 1024;

        // Names WIC is expected to have a decoder for. A file that is not on the list is never
        // decoded, and one that is may still fail - a Windows without the codec, or a name that
        // lies - which is why the result decides and not the extension.
        private static readonly string[] ImageExtensions =
        {
            ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".dib",
            ".tif", ".tiff", ".ico", ".cur", ".wdp", ".hdp", ".jxr", ".webp", ".heic", ".avif"
        };

        /// <summary>
        /// Builds the tree for <paramref name="directory"/>. The callback gets null when the
        /// folder itself cannot be read.
        /// </summary>
        public static void Scan(string directory, Action<FileNodeViewModel> completed)
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            ThreadPool.QueueUserWorkItem(delegate
            {
                FileNodeViewModel root = null;
                try
                {
                    DirectoryInfo info = new DirectoryInfo(directory);
                    root = new FileNodeViewModel(info.Name, info.FullName, true);
                    Fill(root, info);
                    root.IsExpanded = true;
                }
                catch (IOException)
                {
                    root = null;
                }
                catch (UnauthorizedAccessException)
                {
                    root = null;
                }

                dispatcher.BeginInvoke(new Action(delegate { completed(root); }));
            });
        }

        /// <summary>
        /// Opens whatever is at <paramref name="path"/>. Nothing is refused for being the wrong
        /// sort of file: a picture is decoded, text is decoded in the encoding it was written in,
        /// and anything else is shown as a dump of its first bytes.
        /// </summary>
        public static void Open(string path, Action<OpenedFile> completed, Action<string> failed)
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            ThreadPool.QueueUserWorkItem(delegate
            {
                OpenedFile opened = null;
                string error = null;

                try
                {
                    opened = Load(path);
                }
                catch (IOException e)
                {
                    error = e.Message;
                }
                catch (UnauthorizedAccessException e)
                {
                    error = e.Message;
                }
                catch (OutOfMemoryException)
                {
                    error = "There was not enough memory to open this file.";
                }

                dispatcher.BeginInvoke(new Action(delegate
                {
                    if (error != null)
                    {
                        failed(error);
                        return;
                    }

                    completed(opened);
                }));
            });
        }

        private static OpenedFile Load(string path)
        {
            FileInfo info = new FileInfo(path);
            OpenedFile opened = new OpenedFile(path, info.Length);

            if (info.Length <= MaximumImageBytes && HasImageExtension(path))
            {
                BitmapSource image = Decode(path);
                if (image != null)
                {
                    opened.SetImage(image);
                    return opened;
                }
            }

            byte[] head = Head(path, PreviewBytes);
            Encoding encoding = TextEncoding(head, info.Length > head.Length);

            if (encoding != null)
            {
                bool editable = info.Length <= MaximumEditableBytes;

                // One rule, so it explains itself: what can be edited is coloured. The two limits
                // used to differ, which left a band of files that opened for editing with no
                // colours and nothing on screen saying why.
                SyntaxLanguage language = editable ? SyntaxCatalog.For(path) : null;

                opened.SetText(ReadText(path, encoding, editable), encoding, editable, language);
                return opened;
            }

            opened.SetBinary(Dump(head), head.Length);
            return opened;
        }

        private static bool HasImageExtension(string path)
        {
            string extension = Path.GetExtension(path);
            foreach (string candidate in ImageExtensions)
            {
                if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Frozen on this thread, which is what lets it cross to the interface thread.</summary>
        private static BitmapSource Decode(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (NotSupportedException)
            {
                // No decoder for this format on this Windows - it is shown as bytes instead.
                return null;
            }
            catch (FileFormatException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static byte[] Head(string path, int count)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] buffer = new byte[(int)Math.Min(count, stream.Length)];
                int read = 0;
                while (read < buffer.Length)
                {
                    int step = stream.Read(buffer, read, buffer.Length - read);
                    if (step == 0)
                    {
                        break;
                    }

                    read += step;
                }

                if (read == buffer.Length)
                {
                    return buffer;
                }

                byte[] exact = new byte[read];
                Array.Copy(buffer, exact, read);
                return exact;
            }
        }

        /// <summary>
        /// The encoding to read this file in, or null when it is not text at all. A byte order mark
        /// is taken at its word; otherwise a run of bytes that decodes as UTF-8 is UTF-8, and one
        /// that does not is read in the machine's own code page - which is what a batch file or a
        /// log written years ago on this computer actually is.
        /// </summary>
        private static Encoding TextEncoding(byte[] head, bool truncated)
        {
            if (head.Length == 0)
            {
                return new UTF8Encoding(false);
            }

            if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }

            if (head.Length >= 2 && head[0] == 0xFF && head[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (head.Length >= 2 && head[0] == 0xFE && head[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }

            // A zero byte is the one reliable sign of something that is not text. Text with no mark
            // never contains one; an executable, an archive or a picture reaches one almost at once.
            foreach (byte value in head)
            {
                if (value == 0)
                {
                    return null;
                }
            }

            // The tail of a truncated read can cut a character in half, and that must not be what
            // decides the encoding of the whole file.
            int length = truncated ? Math.Max(0, head.Length - 4) : head.Length;

            try
            {
                new UTF8Encoding(false, true).GetString(head, 0, length);
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default;
            }
        }

        private static string ReadText(string path, Encoding encoding, bool whole)
        {
            using (StreamReader reader = new StreamReader(path, encoding, true))
            {
                if (whole)
                {
                    return reader.ReadToEnd();
                }

                char[] buffer = new char[MaximumEditableBytes];
                int read = reader.Read(buffer, 0, buffer.Length);
                return new string(buffer, 0, read);
            }
        }

        /// <summary>Offset, bytes and the printable characters - the usual three columns.</summary>
        private static string Dump(byte[] bytes)
        {
            StringBuilder text = new StringBuilder(bytes.Length * 4);
            StringBuilder characters = new StringBuilder(16);

            for (int offset = 0; offset < bytes.Length; offset += 16)
            {
                text.Append(offset.ToString("x8")).Append("  ");
                characters.Length = 0;

                for (int column = 0; column < 16; column++)
                {
                    if (offset + column < bytes.Length)
                    {
                        byte value = bytes[offset + column];
                        text.Append(value.ToString("x2")).Append(' ');
                        characters.Append(value >= 0x20 && value < 0x7F ? (char)value : '.');
                    }
                    else
                    {
                        text.Append("   ");
                    }

                    if (column == 7)
                    {
                        text.Append(' ');
                    }
                }

                text.Append(' ').Append(characters).Append(Environment.NewLine);
            }

            return text.ToString();
        }

        public static void Write(string path, string text, Encoding encoding, Action completed, Action<string> failed)
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            Encoding writeAs = encoding ?? new UTF8Encoding(false);

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try
                {
                    File.WriteAllText(path, text ?? string.Empty, writeAs);
                }
                catch (IOException e)
                {
                    error = e.Message;
                }
                catch (UnauthorizedAccessException e)
                {
                    error = e.Message;
                }

                dispatcher.BeginInvoke(new Action(delegate
                {
                    if (error != null)
                    {
                        failed(error);
                        return;
                    }

                    completed();
                }));
            });
        }

        private static void Fill(FileNodeViewModel node, DirectoryInfo directory)
        {
            DirectoryInfo[] folders;
            FileInfo[] files;

            try
            {
                folders = directory.GetDirectories();
                files = directory.GetFiles();
            }
            catch (IOException)
            {
                // A folder that refuses to be listed is shown empty rather than failing the
                // whole tree - one unreadable directory must not hide the rest of the project.
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            List<FileNodeViewModel> children = new List<FileNodeViewModel>();

            Array.Sort(folders, delegate(DirectoryInfo left, DirectoryInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            Array.Sort(files, delegate(FileInfo left, FileInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            foreach (DirectoryInfo folder in folders)
            {
                // Hidden folders are the machinery of the folder, not its content: the project's
                // own settings folder and a source-control database both live there.
                if (IsHidden(folder.Attributes))
                {
                    continue;
                }

                FileNodeViewModel child = new FileNodeViewModel(folder.Name, folder.FullName, true);
                Fill(child, folder);
                children.Add(child);
            }

            foreach (FileInfo file in files)
            {
                if (IsHidden(file.Attributes))
                {
                    continue;
                }

                children.Add(new FileNodeViewModel(file.Name, file.FullName, false));
            }

            node.SetChildren(children);
        }

        private static bool IsHidden(FileAttributes attributes)
        {
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
    }
}
