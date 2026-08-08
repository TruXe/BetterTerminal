using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace BetterTerminal.Updating
{
    /// <summary>
    /// The contract shared by the service that checks for updates and the application that applies
    /// them. It is compiled into both assemblies (linked, like VersionInfo.cs) rather than shared
    /// through a reference, because the two projects do not reference each other and a service that
    /// runs as LocalSystem must not depend on the WPF application.
    ///
    /// The two processes meet in exactly two places: a named pipe the service uses to tell a running
    /// application that a newer build is staged, and a pair of small records under ProgramData that
    /// carry the same facts across process restarts. ProgramData is used, not either profile's
    /// AppData, because the service (LocalSystem) and the application (the interactive user) have
    /// different profiles and only a machine-wide folder is readable by both.
    /// </summary>
    internal static class UpdateShared
    {
        public const string RepoOwner = "TruXe";
        public const string RepoName = "BetterTerminal";
        public const string AssetName = "BetterTerminal.exe";

        /// <summary>The pipe the service serves and the application connects to.</summary>
        public const string PipeName = "BetterTerminal.Update";

        /// <summary>Sent by the service when a newer build has been staged.</summary>
        public const string UpdateMessagePrefix = "update ";

        /// <summary>A client asking the service to check now rather than wait for the next poll.</summary>
        public const string CheckRequest = "check";

        public const string NoneReply = "none";

        public const string UserAgent = "BetterTerminal-Updater";

        // Test hook (constraint: off by default). With BETERM_UPDATE_FEED set to a version the check
        // treats that as the latest release and skips the network; BETERM_UPDATE_ASSET then points at
        // a local file to stage instead of downloading. This is what lets both the check and the
        // apply be exercised in a build environment without publishing a release. Neither variable is
        // read in normal operation.
        public const string FeedOverrideVariable = "BETERM_UPDATE_FEED";
        public const string AssetOverrideVariable = "BETERM_UPDATE_ASSET";
        public const string PollSecondsVariable = "BETERM_UPDATE_POLL_SECONDS";

        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromHours(4);

        // A poll runs shortly after the service starts rather than at once, so a machine booting a
        // dozen services does not have this one reach out over the network in the middle of it.
        public static readonly TimeSpan InitialPollDelay = TimeSpan.FromMinutes(1);

        public static string LatestReleaseUrl
        {
            get { return "https://github.com/" + RepoOwner + "/" + RepoName + "/releases/latest"; }
        }

        public static string AssetUrl(string tag)
        {
            return "https://github.com/" + RepoOwner + "/" + RepoName + "/releases/download/" + tag + "/" + AssetName;
        }

        public static string BaseDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "BetterTerminal",
                    "update");
            }
        }

        public static string StagingDirectory
        {
            get { return Path.Combine(BaseDirectory, "staging"); }
        }

        public static string InstalledRecordPath
        {
            get { return Path.Combine(BaseDirectory, "installed.txt"); }
        }

        public static string StagedRecordPath
        {
            get { return Path.Combine(BaseDirectory, "staged.txt"); }
        }

        public static TimeSpan PollInterval
        {
            get
            {
                int seconds;
                string value = Environment.GetEnvironmentVariable(PollSecondsVariable);
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out seconds) && seconds > 0)
                {
                    return TimeSpan.FromSeconds(seconds);
                }

                return DefaultPollInterval;
            }
        }

        /// <summary>The version in a release tag such as "v1.3.0", or null when it is not a version.</summary>
        public static Version ParseTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            string trimmed = tag.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == 'v' || trimmed[0] == 'V'))
            {
                trimmed = trimmed.Substring(1);
            }

            Version version;
            return Version.TryParse(trimmed, out version) ? Normalize(version) : null;
        }

        /// <summary>
        /// A four-part version with the unspecified parts pinned to zero, so a tag "1.3.0" and a file
        /// version "1.3.0.0" compare equal instead of the tag reading as the smaller of the two.
        /// </summary>
        public static Version Normalize(Version version)
        {
            if (version == null)
            {
                return new Version(0, 0, 0, 0);
            }

            return new Version(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build),
                Math.Max(0, version.Revision));
        }

        public static bool IsNewer(Version candidate, Version baseline)
        {
            return Normalize(candidate) > Normalize(baseline);
        }

        public static Version FileVersion(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                string version = FileVersionInfo.GetVersionInfo(path).FileVersion;
                Version parsed;
                return Version.TryParse(version, out parsed) ? Normalize(parsed) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void WriteInstalled(Version version, string executable)
        {
            Write(InstalledRecordPath,
                "version=" + Normalize(version) + Environment.NewLine +
                "exe=" + executable + Environment.NewLine);
        }

        public static void WriteStaged(Version version, string launcher)
        {
            Write(StagedRecordPath,
                "version=" + Normalize(version) + Environment.NewLine +
                "launcher=" + launcher + Environment.NewLine);
        }

        public static Version ReadInstalledVersion()
        {
            return ReadVersion(InstalledRecordPath);
        }

        public static Version ReadStagedVersion()
        {
            return ReadVersion(StagedRecordPath);
        }

        /// <summary>
        /// The staged launcher for a version, but only when the record still names that version and
        /// the file is actually there. A record left pointing at a file that was cleaned up returns
        /// null, so a stale record never sends the application at a launcher that no longer exists.
        /// </summary>
        public static string ReadStagedLauncher(Version expected)
        {
            string launcher = ReadField(StagedRecordPath, "launcher");
            Version staged = ReadStagedVersion();
            if (launcher == null || staged == null || !File.Exists(launcher))
            {
                return null;
            }

            return Normalize(staged) == Normalize(expected) ? launcher : null;
        }

        public static void ClearStaged()
        {
            try
            {
                if (File.Exists(StagedRecordPath))
                {
                    File.Delete(StagedRecordPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static Version ReadVersion(string path)
        {
            string value = ReadField(path, "version");
            Version version;
            return value != null && Version.TryParse(value, out version) ? Normalize(version) : null;
        }

        private static string ReadField(string path, string key)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                foreach (string line in File.ReadAllLines(path))
                {
                    int split = line.IndexOf('=');
                    if (split > 0 &&
                        string.Equals(line.Substring(0, split), key, StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring(split + 1).Trim();
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        private static void Write(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, content);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static string NormalizedString(Version version)
        {
            return Normalize(version).ToString(4);
        }

        public static string StagedFileName(Version version)
        {
            return "BetterTerminal-" + Normalize(version).ToString(4) + ".exe";
        }

        internal static string FeedOverride()
        {
            return Environment.GetEnvironmentVariable(FeedOverrideVariable);
        }

        internal static string AssetOverride()
        {
            return Environment.GetEnvironmentVariable(AssetOverrideVariable);
        }

        internal static bool LooksLikeLocalPath(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                (value.IndexOf("://", StringComparison.Ordinal) < 0);
        }

        internal static CultureInfo Invariant
        {
            get { return CultureInfo.InvariantCulture; }
        }
    }
}
