using System;
using System.Collections.Generic;

namespace BetterTerminal.Terminal
{
    public sealed class TerminalLinkOptions
    {
        private static readonly char[] SchemeSeparators = { ',', ';', ' ', '\t' };

        private readonly List<string> _allowedSchemes = DefaultSchemes();

        public TerminalLinkOptions()
        {
            DetectionEnabled = true;
            ConfirmMisleading = true;
            Activation = LinkActivation.Control;
        }

        public bool DetectionEnabled { get; set; }

        public bool ConfirmMisleading { get; set; }

        public LinkActivation Activation { get; set; }

        public IList<string> AllowedSchemes
        {
            get { return _allowedSchemes; }
        }

        public string SchemeText
        {
            get { return string.Join(", ", _allowedSchemes.ToArray()); }
        }

        public static List<string> DefaultSchemes()
        {
            return new List<string> { "http", "https", "mailto", "file" };
        }

        public static List<string> ParseSchemes(string text)
        {
            List<string> schemes = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return DefaultSchemes();
            }

            foreach (string entry in text.Split(SchemeSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                string scheme = entry.Trim().TrimEnd('/').TrimEnd(':').ToLowerInvariant();
                if (scheme.Length > 0 && !schemes.Contains(scheme))
                {
                    schemes.Add(scheme);
                }
            }

            return schemes.Count == 0 ? DefaultSchemes() : schemes;
        }

        public void SetSchemes(IEnumerable<string> schemes)
        {
            _allowedSchemes.Clear();
            if (schemes == null)
            {
                _allowedSchemes.AddRange(DefaultSchemes());
                return;
            }

            foreach (string scheme in schemes)
            {
                if (!string.IsNullOrEmpty(scheme))
                {
                    _allowedSchemes.Add(scheme.Trim().ToLowerInvariant());
                }
            }

            if (_allowedSchemes.Count == 0)
            {
                _allowedSchemes.AddRange(DefaultSchemes());
            }
        }
    }
}
