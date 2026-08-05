using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// .NET Framework 4.8 has no ThemeMode / UISettings binding, so the OS app-theme
    /// preference is read from its registry value. Any failure falls back to dark.
    /// </summary>
    internal static class SystemPreference
    {
        private const string PersonalizeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool IsAppsUseLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object value = key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 1;
                }
            }
            catch (SecurityException)
            {
                // A policy-locked or unreadable hive is not an error worth failing startup over:
                // the shipped default is dark, which is what a missing preference means anyway.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
