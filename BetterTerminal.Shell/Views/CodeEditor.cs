using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using BetterTerminal.Shell.Services;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// A code view with colours. One paragraph per line, each holding one run per coloured stretch,
    /// and the state left open at the end of a line is kept on the next one - so typing re-reads a
    /// single line, and only opening or closing a comment or an element re-reads what follows.
    ///
    /// It is a rich text box because that is the one control in this framework that can both be
    /// edited and hold colour. The cost is that a colour change is an edit like any other, so the
    /// undo history has entries in it that the user did not type.
    /// </summary>
    public class CodeEditor : RichTextBox
    {
        public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
            "SourceText", typeof(string), typeof(CodeEditor),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSourceTextChanged));

        public static readonly DependencyProperty SyntaxProperty = DependencyProperty.Register(
            "Syntax", typeof(SyntaxLanguage), typeof(CodeEditor),
            new PropertyMetadata(null, OnSyntaxChanged));

        private static readonly Dictionary<TokenKind, string> BrushKeys = new Dictionary<TokenKind, string>
        {
            { TokenKind.Comment, "Bt.Syntax.CommentBrush" },
            { TokenKind.String, "Bt.Syntax.StringBrush" },
            { TokenKind.Number, "Bt.Syntax.NumberBrush" },
            { TokenKind.Keyword, "Bt.Syntax.KeywordBrush" },
            { TokenKind.Tag, "Bt.Syntax.TagBrush" },
            { TokenKind.Attribute, "Bt.Syntax.AttributeBrush" },
            { TokenKind.Property, "Bt.Syntax.PropertyBrush" },
            { TokenKind.Operator, "Bt.Syntax.OperatorBrush" }
        };

        private readonly List<SyntaxToken> _tokens = new List<SyntaxToken>();
        private readonly DispatcherTimer _pause;
        private bool _building;
        private string _lineBreak = "\r\n";

        public CodeEditor()
        {
            AcceptsTab = true;
            AutoWordSelection = false;
            SpellCheck.IsEnabled = false;

            // Colouring on every keystroke would fight the typing; a short pause is long enough to
            // feel immediate and short enough that nothing is ever re-read twice. It is built
            // before the document, because assigning one already raises a text change.
            _pause = new DispatcherTimer(DispatcherPriority.Background);
            _pause.Interval = TimeSpan.FromMilliseconds(220);
            _pause.Tick += OnPause;

            Document = NewDocument();
        }

        public string SourceText
        {
            get { return (string)GetValue(SourceTextProperty); }
            set { SetValue(SourceTextProperty, value); }
        }

        public SyntaxLanguage Syntax
        {
            get { return (SyntaxLanguage)GetValue(SyntaxProperty); }
            set { SetValue(SyntaxProperty, value); }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);

            if (_building)
            {
                return;
            }

            _building = true;
            SetCurrentValue(SourceTextProperty, ReadDocument());
            _building = false;

            _pause.Stop();
            _pause.Start();
        }

        private static void OnSourceTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            CodeEditor editor = (CodeEditor)sender;
            if (!editor._building)
            {
                editor.Build((string)e.NewValue);
            }
        }

        private static void OnSyntaxChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            CodeEditor editor = (CodeEditor)sender;
            editor.Build(editor.SourceText);
        }

        private static FlowDocument NewDocument()
        {
            FlowDocument document = new FlowDocument();
            document.PagePadding = new Thickness(0);
            // Wrapping code hides its shape; a wide page and the horizontal bar keep the lines whole.
            document.PageWidth = 4000;
            return document;
        }

        /// <summary>Rebuilds the whole view. Only on load, or when the language changes.</summary>
        private void Build(string text)
        {
            _building = true;
            try
            {
                _lineBreak = text != null && text.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                    ? "\r\n"
                    : "\n";

                FlowDocument document = NewDocument();
                SyntaxState state = SyntaxState.Normal;

                foreach (string line in (text ?? string.Empty).Split('\n'))
                {
                    Paragraph paragraph = new Paragraph();
                    paragraph.Margin = new Thickness(0);
                    paragraph.Tag = state;
                    state = Fill(paragraph, line.TrimEnd('\r'), state);
                    document.Blocks.Add(paragraph);
                }

                Document = document;
                CaretPosition = document.ContentStart;
            }
            finally
            {
                _building = false;
            }
        }

        /// <summary>Lays the coloured runs of one line into a paragraph, replacing what was there.</summary>
        private SyntaxState Fill(Paragraph paragraph, string line, SyntaxState state)
        {
            paragraph.Inlines.Clear();

            SyntaxState next = SyntaxHighlighter.Read(line, Syntax, state, _tokens);
            int at = 0;

            foreach (SyntaxToken token in _tokens)
            {
                if (token.Start > at)
                {
                    paragraph.Inlines.Add(new Run(line.Substring(at, token.Start - at)));
                }

                Run run = new Run(line.Substring(token.Start, token.Length));
                Brush brush = BrushFor(token.Kind);
                if (brush != null)
                {
                    run.Foreground = brush;
                }

                paragraph.Inlines.Add(run);
                at = token.Start + token.Length;
            }

            if (at < line.Length)
            {
                paragraph.Inlines.Add(new Run(line.Substring(at)));
            }

            return next;
        }

        private Brush BrushFor(TokenKind kind)
        {
            string key;
            if (!BrushKeys.TryGetValue(kind, out key))
            {
                return null;
            }

            return TryFindResource(key) as Brush;
        }

        /// <summary>
        /// Re-reads the line the caret is on, and the ones below it only when that line changed
        /// what it leaves open - a newly typed comment marker, an element opened or closed.
        /// </summary>
        private void OnPause(object sender, EventArgs e)
        {
            _pause.Stop();

            if (Syntax == null || _building)
            {
                return;
            }

            Paragraph paragraph = CaretPosition == null ? null : CaretPosition.Paragraph;
            if (paragraph == null)
            {
                return;
            }

            _building = true;
            try
            {
                int caret = new TextRange(paragraph.ContentStart, CaretPosition).Text.Length;
                SyntaxState before = paragraph.Tag is SyntaxState ? (SyntaxState)paragraph.Tag : SyntaxState.Normal;
                SyntaxState after = Fill(paragraph, TextOf(paragraph), before);

                CaretPosition = PositionIn(paragraph, caret);

                Block block = paragraph.NextBlock;
                if (block is Paragraph && !Equals(((Paragraph)block).Tag, after))
                {
                    while (block is Paragraph)
                    {
                        Paragraph below = (Paragraph)block;
                        below.Tag = after;
                        after = Fill(below, TextOf(below), after);
                        block = below.NextBlock;
                    }
                }
            }
            finally
            {
                _building = false;
            }
        }

        private static string TextOf(Paragraph paragraph)
        {
            return new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        }

        private string ReadDocument()
        {
            StringBuilder text = new StringBuilder();
            bool first = true;

            foreach (Block block in Document.Blocks)
            {
                Paragraph paragraph = block as Paragraph;
                if (paragraph == null)
                {
                    continue;
                }

                if (!first)
                {
                    text.Append(_lineBreak);
                }

                text.Append(TextOf(paragraph));
                first = false;
            }

            return text.ToString();
        }

        /// <summary>
        /// The position this many characters into a paragraph. Counted by text length rather than
        /// by offset, because every run boundary counts as a position of its own and the number of
        /// runs is exactly what colouring changes.
        /// </summary>
        private static TextPointer PositionIn(Paragraph paragraph, int characters)
        {
            TextPointer position = paragraph.ContentStart;

            while (position != null && position.Paragraph == paragraph)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    int run = position.GetTextRunLength(LogicalDirection.Forward);
                    if (run >= characters)
                    {
                        return position.GetPositionAtOffset(characters, LogicalDirection.Forward);
                    }

                    characters -= run;
                }

                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }

            return paragraph.ContentEnd;
        }
    }
}
