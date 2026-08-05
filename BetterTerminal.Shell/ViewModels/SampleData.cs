using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// Design-time content only. Realistic records: real paths, a long value per text column,
    /// a failing exit code, a negative number, and mixed locales (BP-R1, BP-R2). Never "Item 1".
    /// </summary>
    internal static class SampleData
    {
        private static Brush Hex(string value)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        public static void Populate(MainViewModel model)
        {
            PaneViewModel powershell = new PaneViewModel
            {
                IsFocused = true,
                WorkingDirectory = @"C:\src\betterterminal\src\BetterTerminal",
                ShellDescription = "Windows PowerShell",
                BadgeText = "powershell",
                LastExitCode = 0,
                DesignTimePreview =
                    "PS C:\\src\\betterterminal> git status\r\n" +
                    "On branch feature/pane-splitting\r\n" +
                    "Changes not staged for commit:\r\n" +
                    "        modified:   src/BetterTerminal/Themes/Tokens.Dark.xaml\r\n" +
                    "        modified:   src/BetterTerminal/Views/TerminalSurface.cs\r\n"
            };

            PaneViewModel prompt = new PaneViewModel
            {
                WorkingDirectory = @"C:\Windows\system32",
                ShellDescription = "Command Prompt",
                BadgeText = "cmd",
                LastExitCode = 0,
                DesignTimePreview =
                    "C:\\Users\\frant>ver\r\n" +
                    "Microsoft Windows [Version 10.0.22631.6199]\r\n"
            };

            PaneViewModel failing = new PaneViewModel
            {
                WorkingDirectory = @"D:\Multi Terminál Window\BetterTerminal.Shell",
                ShellDescription = "Command Prompt",
                BadgeText = "exit 1",
                LastExitCode = 1,
                DesignTimePreview =
                    "C:\\src\\deploy>publish.cmd --platform x64\r\n" +
                    "  TerminalSurface.cs(214,17): error CS0246: type 'PaneSplitOrientation' not found\r\n" +
                    "Build FAILED.  1 Error(s)\r\n"
            };

            RowSplitViewModel rightColumn = new RowSplitViewModel
            {
                First = prompt,
                Second = failing
            };

            ColumnSplitViewModel root = new ColumnSplitViewModel
            {
                First = powershell,
                Second = rightColumn
            };

            model.Tabs.Add(new TabViewModel
            {
                Title = "powershell - betterterminal",
                FullTitle = @"Windows PowerShell - C:\src\betterterminal\src\BetterTerminal",
                RootPane = root,
                FocusedPane = powershell
            });
            model.Tabs.Add(new TabViewModel
            {
                Title = "cmd - system32",
                FullTitle = @"Command Prompt - C:\Windows\system32",
                RootPane = prompt,
                FocusedPane = prompt
            });
            model.Tabs.Add(new TabViewModel
            {
                Title = "cmd - deploy",
                FullTitle = @"Command Prompt - D:\Multi Terminál Window\BetterTerminal.Shell",
                HasFailed = true,
                LastExitDescription = "Last command exited with code 1",
                RootPane = failing,
                FocusedPane = failing
            });
            model.SelectedTab = model.Tabs[0];

            foreach (ProfileViewModel profile in Profiles())
            {
                model.Profiles.Add(profile);
            }

            model.DefaultProfile = model.Profiles[0];
        }

        public static ProfileViewModel[] Profiles()
        {
            return new[]
            {
                new ProfileViewModel
                {
                    Name = "Windows PowerShell",
                    CommandLine = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe -NoLogo",
                    StartingDirectory = @"C:\src\betterterminal",
                    Source = "built-in", Accelerator = "Ctrl+Shift+1", ScrollbackLines = 5000
                },
                new ProfileViewModel
                {
                    Name = "Command Prompt",
                    CommandLine = @"C:\Windows\system32\cmd.exe",
                    StartingDirectory = @"C:\Users\frant",
                    Source = "built-in", Accelerator = "Ctrl+Shift+2", ScrollbackLines = 5000
                },
                // Long value in the text column, per BP-R2.
                new ProfileViewModel
                {
                    Name = "Developer Command Prompt",
                    CommandLine = @"cmd.exe /k ""C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat""",
                    StartingDirectory = @"D:\Multi Terminál Window",
                    Source = "detected", Accelerator = "Ctrl+Shift+3", ScrollbackLines = 5000
                },
                // Mixed locale, per BP-R1.
                new ProfileViewModel
                {
                    Name = "Účetní export (Kč)",
                    CommandLine = @"powershell.exe -NoLogo -File D:\ucto\export-2026.ps1",
                    StartingDirectory = @"D:\ucto",
                    Source = "user", Accelerator = "Ctrl+Shift+4",
                    RunAsAdministrator = true, ScrollbackLines = 4000
                }
            };
        }

        public static void Populate(SettingsViewModel model)
        {
            model.FontSize = 14;
            model.IsCursorBlock = true;
            model.BlinkCursor = true;

            model.Schemes.Add(Scheme("Campbell", "Campbell", "#FF0C0C0C", "#FFCCCCCC",
                new[] { "#FF0C0C0C", "#FFC50F1F", "#FF13A10E", "#FFC19C00", "#FF0037DA", "#FF881798", "#FF3A96DD", "#FFCCCCCC" }));
            model.Schemes.Add(Scheme("One Half Dark", "OneHalfDark", "#FF282C34", "#FFDCDFE4",
                new[] { "#FF282C34", "#FFE06C75", "#FF98C379", "#FFE5C07B", "#FF61AFEF", "#FFC678DD", "#FF56B6C2", "#FFDCDFE4" }));
            model.Schemes.Add(Scheme("Solarized Dark", "SolarizedDark", "#FF002B36", "#FF839496",
                new[] { "#FF002B36", "#FFDC322F", "#FF859900", "#FFB58900", "#FF268BD2", "#FFD33682", "#FF2AA198", "#FFEEE8D5" }));

            model.SelectedScheme = model.Schemes[0];
        }

        public static SchemeViewModel Scheme(string name, string dictionaryName, string background,
            string foreground, string[] swatches)
        {
            ObservableCollection<Brush> brushes = new ObservableCollection<Brush>();
            foreach (string value in swatches)
            {
                brushes.Add(Hex(value));
            }

            return new SchemeViewModel
            {
                Name = name,
                DictionaryName = dictionaryName,
                BackgroundBrush = Hex(background),
                ForegroundBrush = Hex(foreground),
                Background = (Color)ColorConverter.ConvertFromString(background),
                Foreground = (Color)ColorConverter.ConvertFromString(foreground),
                Cursor = (Color)ColorConverter.ConvertFromString(foreground),
                Selection = (Color)ColorConverter.ConvertFromString("#FF3A3D41"),
                Swatches = brushes,
                PreviewText =
                    "C:\\src>dir /b *.sln\r\n" +
                    "BetterTerminal.sln\r\n" +
                    "C:\\src>type nul > out.log\r\n" +
                    "delta -1 048 576 bytes\r\n" +
                    "C:\\src>"
            };
        }

        public static void Populate(CommandPaletteViewModel model)
        {
            Add(model, "Split pane right", "Panes", "\uEE3F", "Alt+Shift+Plus");
            Add(model, "Split pane down", "Panes", "\uEE40", "Alt+Shift+Minus");
            Add(model, "Move focus to the next split", "Navigation", "\uE72A", "Alt+Right");
            Add(model, "New tab", "Tabs", "\uE710", "Ctrl+Shift+T");
            Add(model, "Close pane", "Panes", "\uE89F", "Ctrl+Shift+W");
            Add(model, "Open settings", "Application", "\uE713", "Ctrl+comma");
            model.Query = "split";
        }

        private static void Add(CommandPaletteViewModel model, string name, string group, string glyph, string keys)
        {
            model.All.Add(new CommandItemViewModel
            {
                Name = name,
                Group = group,
                Glyph = glyph,
                KeysDisplay = keys,
                Source = "built-in"
            });
        }
    }
}
