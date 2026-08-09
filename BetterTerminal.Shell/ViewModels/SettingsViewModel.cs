using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BetterTerminal.Shell.Services;

namespace BetterTerminal.Shell.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private SchemeViewModel _selectedScheme;
        private SettingsPageViewModel _selectedPage;
        private AppTheme _theme = AppTheme.Dark;
        private string _selectedFont;
        private int _fontSize = 14;
        private bool _isCursorBlock = true;
        private bool _isCursorBar;
        private bool _isCursorUnderline;
        private bool _blinkCursor = true;
        private bool _splitUsesActiveProfile;
        private bool _showInFolderMenu;

        public SettingsViewModel()
        {
            Pages = new ObservableCollection<SettingsPageViewModel>
            {
                new SettingsPageViewModel { Title = "Appearance", Glyph = "\uE790" },
                new SettingsPageViewModel { Title = "Profiles", Glyph = "\uE756" },
                new SettingsPageViewModel { Title = "Keyboard", Glyph = "\uE765" },
                new SettingsPageViewModel { Title = "Panes and tabs", Glyph = "\uEE3F" },
                new SettingsPageViewModel { Title = "Integration", Glyph = "\uE8B7" },
                new SettingsPageViewModel { Title = "About", Glyph = "\uE946" }
            };
            _selectedPage = Pages[0];

            // The right-click entry lives in the registry and nowhere else, so the switch starts
            // from what is actually registered rather than from a stored preference.
            _showInFolderMenu = ExplorerMenu.IsVisible;

            Schemes = new ObservableCollection<SchemeViewModel>();
            Profiles = new ObservableCollection<ProfileViewModel>();
            Shortcuts = new ObservableCollection<ShortcutViewModel>();
            MonoFonts = new ObservableCollection<string>
            {
                "Cascadia Mono", "Cascadia Code", "Consolas", "Lucida Console"
            };
            _selectedFont = MonoFonts[0];

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                SampleData.Populate(this);
            }
        }

        /// <summary>Raised whenever a setting that the terminal surfaces must honour changed.</summary>
        public event EventHandler Changed;

        public ObservableCollection<SettingsPageViewModel> Pages { get; private set; }

        public SettingsPageViewModel SelectedPage
        {
            get { return _selectedPage; }
            set { Set(ref _selectedPage, value); }
        }

        public ObservableCollection<SchemeViewModel> Schemes { get; private set; }

        public SchemeViewModel SelectedScheme
        {
            get { return _selectedScheme; }

            set
            {
                if (!Set(ref _selectedScheme, value) || value == null)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(value.DictionaryName))
                {
                    ThemeService.Current.SchemeName = value.DictionaryName;
                }

                RaiseChanged();
            }
        }

        /// <summary>The profiles the shell can launch. Filled by TerminalWorkspace.</summary>
        public ObservableCollection<ProfileViewModel> Profiles { get; private set; }

        /// <summary>Every key binding the shell actually installs. Filled by TerminalWorkspace.</summary>
        public ObservableCollection<ShortcutViewModel> Shortcuts { get; private set; }

        /// <summary>Facts for the About page. Filled by TerminalWorkspace.</summary>
        public AboutViewModel About { get; set; }

        /// <summary>
        /// False (the default) means a split starts the profile chosen in the command bar;
        /// true means it repeats the profile of the pane being split.
        /// </summary>
        public bool SplitUsesActiveProfile
        {
            get { return _splitUsesActiveProfile; }

            set
            {
                if (Set(ref _splitUsesActiveProfile, value))
                {
                    Raise("SplitUsesSelectedProfile");
                    RaiseChanged();
                }
            }
        }

        public bool SplitUsesSelectedProfile
        {
            get { return !_splitUsesActiveProfile; }

            set
            {
                if (value)
                {
                    SplitUsesActiveProfile = false;
                }
            }
        }

        /// <summary>
        /// Whether the application is offered in the folder right-click menu. Applied straight
        /// away rather than on a Changed event: the terminal surfaces have nothing to do with it,
        /// and the registry is where the setting lives.
        /// </summary>
        public bool ShowInFolderMenu
        {
            get { return _showInFolderMenu; }

            set
            {
                if (_showInFolderMenu == value)
                {
                    return;
                }

                // Take the value back from the system, not from the switch: an entry that could
                // not be written must show as absent.
                _showInFolderMenu = ExplorerMenu.SetVisible(value);
                Raise("ShowInFolderMenu");
            }
        }

        /// <summary>The command line the menu entry runs, shown so it can be checked.</summary>
        public string FolderMenuCommand
        {
            get { return ExplorerMenu.CommandLine(); }
        }

        public ObservableCollection<string> MonoFonts { get; private set; }

        public string SelectedFont
        {
            get { return _selectedFont; }

            set
            {
                if (Set(ref _selectedFont, value))
                {
                    RaiseChanged();
                }
            }
        }

        public int FontSize
        {
            get { return _fontSize; }

            set
            {
                int clamped = Math.Max(8, Math.Min(36, value));
                if (Set(ref _fontSize, clamped))
                {
                    RaiseChanged();
                }
            }
        }

        public bool IsCursorBar
        {
            get { return _isCursorBar; }
            set { SetCursorShape(ref _isCursorBar, value); }
        }

        public bool IsCursorBlock
        {
            get { return _isCursorBlock; }
            set { SetCursorShape(ref _isCursorBlock, value); }
        }

        public bool IsCursorUnderline
        {
            get { return _isCursorUnderline; }
            set { SetCursorShape(ref _isCursorUnderline, value); }
        }

        public bool BlinkCursor
        {
            get { return _blinkCursor; }

            set
            {
                if (Set(ref _blinkCursor, value))
                {
                    RaiseChanged();
                }
            }
        }

        public bool IsDarkSelected
        {
            get { return _theme == AppTheme.Dark; }

            set
            {
                if (value)
                {
                    SetTheme(AppTheme.Dark);
                }
            }
        }

        public bool IsLightSelected
        {
            get { return _theme == AppTheme.Light; }

            set
            {
                if (value)
                {
                    SetTheme(AppTheme.Light);
                }
            }
        }

        public bool IsFollowSystemSelected
        {
            get { return _theme == AppTheme.FollowSystem; }

            set
            {
                if (value)
                {
                    SetTheme(AppTheme.FollowSystem);
                }
            }
        }

        public ICommand OpenSettingsFileCommand { get; set; }

        public AppTheme Theme
        {
            get { return _theme; }
        }

        /// <summary>
        /// Applies a stored appearance without echoing a change event per property: the caller
        /// re-applies everything to the live surfaces once when it is done.
        /// </summary>
        public void ApplyStored(string theme, string schemeDictionary, string fontFamily,
            int fontSize, string cursorShape, bool blinkCursor)
        {
            AppTheme parsed;
            if (!string.IsNullOrEmpty(theme) && TryParseTheme(theme, out parsed))
            {
                SetTheme(parsed);
            }

            if (!string.IsNullOrEmpty(fontFamily) && MonoFonts.Contains(fontFamily))
            {
                _selectedFont = fontFamily;
                Raise("SelectedFont");
            }

            if (fontSize >= 8 && fontSize <= 36)
            {
                _fontSize = fontSize;
                Raise("FontSize");
            }

            _isCursorBar = cursorShape == "Bar";
            _isCursorUnderline = cursorShape == "Underline";
            _isCursorBlock = !_isCursorBar && !_isCursorUnderline;
            Raise("IsCursorBar");
            Raise("IsCursorBlock");
            Raise("IsCursorUnderline");

            _blinkCursor = blinkCursor;
            Raise("BlinkCursor");

            foreach (SchemeViewModel scheme in Schemes)
            {
                if (scheme.DictionaryName == schemeDictionary)
                {
                    SelectedScheme = scheme;
                    break;
                }
            }
        }

        private static bool TryParseTheme(string value, out AppTheme theme)
        {
            if (value == AppTheme.Light.ToString())
            {
                theme = AppTheme.Light;
                return true;
            }

            if (value == AppTheme.FollowSystem.ToString())
            {
                theme = AppTheme.FollowSystem;
                return true;
            }

            theme = AppTheme.Dark;
            return value == AppTheme.Dark.ToString();
        }

        public string CursorShapeName
        {
            get
            {
                if (_isCursorBar)
                {
                    return "Bar";
                }

                return _isCursorUnderline ? "Underline" : "Block";
            }
        }

        private void SetCursorShape(ref bool field, bool value)
        {
            if (!value || field)
            {
                field = value;
                return;
            }

            _isCursorBar = false;
            _isCursorBlock = false;
            _isCursorUnderline = false;
            field = true;

            Raise("IsCursorBar");
            Raise("IsCursorBlock");
            Raise("IsCursorUnderline");
            RaiseChanged();
        }

        private void SetTheme(AppTheme theme)
        {
            _theme = theme;
            ThemeService.Current.Theme = theme;
            Raise("IsDarkSelected");
            Raise("IsLightSelected");
            Raise("IsFollowSystemSelected");
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
