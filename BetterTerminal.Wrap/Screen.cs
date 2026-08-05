using System;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// One full-screen view. Render draws the whole frame - there is no partial update, because a
    /// resize or a scroll invalidates everything anyway - and HandleKey returns the screen to show
    /// next, or itself to stay.
    ///
    /// The chrome is drawn here for every screen: a header band, the body, and a key bar whose
    /// key names are picked out in the accent colour.
    /// </summary>
    public abstract class Screen
    {
        private const string Product = "BetterTerminal";

        public abstract string Title { get; }

        /// <summary>What the header says on the right. Empty when there is nothing to say.</summary>
        public virtual string Context
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// The keys named at the bottom, as "key=what it does" pairs separated by two spaces.
        /// Concrete, in the order shown.
        /// </summary>
        public abstract string KeyHelp { get; }

        public abstract void RenderBody(AnsiWriter writer, int top, int width, int height);

        public abstract Screen HandleKey(ConsoleKeyInfo key);

        /// <summary>
        /// Called on every loop pass. Returns true when something changed that the frame does not
        /// show yet - live output arriving, a child exiting, a spinner turning.
        /// </summary>
        public virtual bool Poll()
        {
            return false;
        }

        public void Render(AnsiWriter writer, int width, int height)
        {
            writer.Background(Palette.Window);
            writer.Clear();

            RenderHeader(writer, width);

            int bodyTop = 2;
            int bodyHeight = Math.Max(1, height - 4);
            RenderBody(writer, bodyTop, width, bodyHeight);

            RenderKeyBar(writer, height - 1, width);

            writer.ResetAttributes();
            writer.Flush();
        }

        private void RenderHeader(AnsiWriter writer, int width)
        {
            writer.Fill(0, 0, width, Palette.Chrome);

            // The product sits in an accent chip, the way the application's own title bar reads.
            writer.MoveTo(0, 0);
            writer.Background(Palette.Accent);
            writer.Foreground(Palette.AccentInk);
            writer.Write(" " + Product + " ");

            writer.Background(Palette.Chrome);
            writer.Foreground(Palette.TextPrimary);
            writer.Write("  ");
            writer.WriteClipped(Title, width - Product.Length - 6);

            string context = Context;
            if (!string.IsNullOrEmpty(context) && context.Length + 2 < width)
            {
                writer.MoveTo(0, width - context.Length - 1);
                writer.Foreground(Palette.TextTertiary);
                writer.Write(context);
            }

            writer.Fill(1, 0, width, Palette.Window);
        }

        private void RenderKeyBar(AnsiWriter writer, int row, int width)
        {
            writer.Fill(row, 0, width, Palette.Chrome);
            writer.MoveTo(row, 1);

            foreach (string pair in KeyHelp.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries))
            {
                int split = pair.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                writer.Background(Palette.Elevated);
                writer.Foreground(Palette.AccentLight);
                writer.Write(" " + pair.Substring(0, split) + " ");

                writer.Background(Palette.Chrome);
                writer.Foreground(Palette.TextSecondary);
                writer.Write(" " + pair.Substring(split + 1) + "   ");
            }
        }

        /// <summary>A label and its value on one row, the label in the quieter colour.</summary>
        protected static void WriteField(AnsiWriter writer, int row, int column, int labelWidth,
            string label, string value, int width, TerminalColor valueColour)
        {
            writer.MoveTo(row, column);
            writer.Foreground(Palette.TextTertiary);
            writer.WriteClipped(label, labelWidth);

            writer.MoveTo(row, column + labelWidth);
            writer.Foreground(valueColour);
            writer.WriteClipped(value, width - column - labelWidth);
        }
    }
}
