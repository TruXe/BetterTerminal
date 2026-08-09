using System;
using System.IO;
using Microsoft.Win32;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// The application's entry in the folder right-click menu. It is a small set of per-user class
    /// registrations - nothing machine wide, no elevation and no installer, exactly like the
    /// command registration beside it, and removing it is deleting two keys.
    ///
    /// Two places carry the same entry, because which menu a folder shows depends on what was
    /// clicked: the empty background of an open folder, and a folder icon in a list. Both hand the
    /// clicked directory to the application through the same switch the command shim uses, so the
    /// menu and the typed command open a folder the same way.
    ///
    /// The registry is the only record of this setting. Nothing is mirrored into the workspace
    /// file: two records of one fact drift apart the moment the user removes the entry by hand,
    /// and the switch in the settings window would then be describing something that is not there.
    /// </summary>
    public static class ExplorerMenu
    {
        /// <summary>What the entry reads in the menu - the name alone, as its neighbours do.</summary>
        public const string EntryLabel = "BetterTerminal";

        private const string KeyName = "BetterTerminal";
        private const string IconValue = "Icon";
        private const string CommandKey = "command";

        private static readonly string[] Parents =
        {
            @"Software\Classes\Directory\Background\shell",
            @"Software\Classes\Directory\shell"
        };

        /// <summary>True when the entry is registered for this user.</summary>
        public static bool IsVisible
        {
            get
            {
                try
                {
                    foreach (string parent in Parents)
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(parent + "\\" + KeyName))
                        {
                            if (key == null)
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
                catch (System.Security.SecurityException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Adds or removes the entry and reports what the registry holds afterwards, rather than
        /// what was asked for: a write that did not happen must not leave the switch lying.
        /// </summary>
        public static bool SetVisible(bool visible)
        {
            try
            {
                if (visible)
                {
                    Write();
                }
                else
                {
                    Remove();
                }
            }
            catch (IOException)
            {
                // Best effort, like every other registration this application makes: a failure
                // costs the user a menu entry and nothing else.
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }

            return IsVisible;
        }

        /// <summary>
        /// Called on every start. Points an entry the user already asked for at the copy this
        /// build installed, which is what keeps it working across an update; it never adds one.
        /// </summary>
        public static void Refresh()
        {
            if (!IsVisible)
            {
                return;
            }

            SetVisible(true);
        }

        /// <summary>The command line the entry runs, for the settings window to show.</summary>
        public static string CommandLine()
        {
            string target = Target();
            return target == null
                ? string.Empty
                : "\"" + target + "\" " + StartupOptions.ProjectSwitch + " \"%V\"";
        }

        /// <summary>
        /// The copy under the user profile, because that is the one that survives the build folder
        /// being moved or deleted and the one an update replaces. A build that has not installed
        /// itself yet registers itself.
        /// </summary>
        private static string Target()
        {
            string installed = SelfInstall.InstalledExecutable;
            if (File.Exists(installed))
            {
                return installed;
            }

            string running = typeof(ExplorerMenu).Assembly.Location;
            return File.Exists(running) ? running : null;
        }

        private static void Write()
        {
            string target = Target();
            if (target == null)
            {
                return;
            }

            // The icon is the application's own, read out of the executable itself, so the entry
            // carries the same mark as the window and no image file has to be installed anywhere.
            string icon = "\"" + target + "\",0";
            string command = CommandLine();

            foreach (string parent in Parents)
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(parent + "\\" + KeyName))
                {
                    if (key == null)
                    {
                        continue;
                    }

                    // The default value of the key is the text the menu shows.
                    SetIfChanged(key, null, EntryLabel);
                    SetIfChanged(key, IconValue, icon);

                    using (RegistryKey child = key.CreateSubKey(CommandKey))
                    {
                        if (child != null)
                        {
                            SetIfChanged(child, null, command);
                        }
                    }
                }
            }
        }

        private static void Remove()
        {
            foreach (string parent in Parents)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(parent, true))
                {
                    if (key != null)
                    {
                        key.DeleteSubKeyTree(KeyName, false);
                    }
                }
            }
        }

        /// <summary>
        /// Writing a value the system already holds is still a change as far as the desktop is
        /// concerned, and this runs on every start - so read first and write only what differs.
        /// </summary>
        private static void SetIfChanged(RegistryKey key, string name, string value)
        {
            string current = key.GetValue(name) as string;
            if (!string.Equals(current, value, StringComparison.Ordinal))
            {
                key.SetValue(name, value, RegistryValueKind.String);
            }
        }
    }
}
