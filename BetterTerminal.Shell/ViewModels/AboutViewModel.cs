using System;
using System.Reflection;
using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    public class AboutViewModel
    {
        public AboutViewModel()
        {
            // Design-time only; AssemblyVersion() replaces it with the real number when the
            // window is opened. Both come from VersionInfo.cs in the end.
            VersionLine = AssemblyVersion();
            Runtime = ".NET Framework";
            Backend = "Virtual terminal";
            HostOs = "Windows";
            SettingsPath = string.Empty;
        }

        public string VersionLine { get; set; }

        public string Runtime { get; set; }

        public string Backend { get; set; }

        public string HostOs { get; set; }

        public string SettingsPath { get; set; }

        public ICommand OpenReleaseNotesCommand { get; set; }

        public ICommand ReportIssueCommand { get; set; }

        public ICommand OpenNoticesCommand { get; set; }

        public ICommand CopyDetailsCommand { get; set; }

        public string ToDetails()
        {
            return "BetterTerminal " + VersionLine + Environment.NewLine
                + "Runtime: " + Runtime + Environment.NewLine
                + "Backend: " + Backend + Environment.NewLine
                + "Host: " + HostOs + Environment.NewLine
                + "Workspace: " + SettingsPath;
        }

        public static string AssemblyVersion()
        {
            AssemblyName name = Assembly.GetExecutingAssembly().GetName();
            return name.Version.ToString(3);
        }
    }
}
