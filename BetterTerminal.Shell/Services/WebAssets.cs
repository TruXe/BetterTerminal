using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// The three files the browser is served, read once from the assembly they are embedded in.
    /// Kept as files rather than string constants so the page can be edited as a page.
    /// </summary>
    internal static class WebAssets
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private static readonly object Gate = new object();

        public static string Html
        {
            get { return Read("index.html"); }
        }

        public static string Css
        {
            get { return Read("app.css"); }
        }

        public static string Js
        {
            get { return Read("app.js"); }
        }

        private static string Read(string name)
        {
            lock (Gate)
            {
                string cached;
                if (Cache.TryGetValue(name, out cached))
                {
                    return cached;
                }

                string resource = "BetterTerminal.Shell.Web." + name;
                using (Stream stream = typeof(WebAssets).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        return string.Empty;
                    }

                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string text = reader.ReadToEnd();
                        Cache[name] = text;
                        return text;
                    }
                }
            }
        }
    }
}
