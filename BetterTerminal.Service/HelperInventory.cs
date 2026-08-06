using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace BetterTerminal.Service
{
    /// <summary>
    /// The helper programs the host is responsible for, and whether each one is staged beside it.
    /// The report is what the host writes to the log on start, so an operator can see at a glance
    /// that the components are in place without opening the folder.
    /// </summary>
    internal static class HelperInventory
    {
        private static readonly string[] Helpers =
        {
            "beterm-banner.exe",
            "beterm-wrap.exe",
            "beterm-aiwizard.exe"
        };

        public static string Describe()
        {
            string folder = HostDirectory();

            StringBuilder report = new StringBuilder();
            report.Append("Components in ").Append(folder).Append(": ");

            bool first = true;
            foreach (string name in Helpers)
            {
                if (!first)
                {
                    report.Append(", ");
                }

                first = false;

                bool present = File.Exists(Path.Combine(folder, name));
                report.Append(name).Append(present ? " present" : " absent");
            }

            return report.ToString();
        }

        private static string HostDirectory()
        {
            string path = Assembly.GetExecutingAssembly().Location;
            return string.IsNullOrEmpty(path)
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(path);
        }
    }
}
