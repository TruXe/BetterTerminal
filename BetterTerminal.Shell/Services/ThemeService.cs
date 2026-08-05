using System;
using System.ComponentModel;
using System.Windows;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Owns the theme dictionary slot and the terminal scheme slot. No other code may touch
    /// MergedDictionaries (DS-06/DS-20). High contrast always wins over the user's choice (BP-R27).
    /// </summary>
    public sealed class ThemeService
    {
        private const int ThemeSlot = 1;
        private const int SchemeSlot = 7;

        private static readonly ThemeService CurrentInstance = new ThemeService();

        private ResourceDictionary _appResources;
        private AppTheme _theme = AppTheme.Dark;
        private string _schemeName = "Campbell";

        public static ThemeService Current
        {
            get { return CurrentInstance; }
        }

        public event EventHandler ThemeChanged;

        public AppTheme Theme
        {
            get { return _theme; }

            set
            {
                if (_theme == value)
                {
                    return;
                }

                _theme = value;
                Apply();
            }
        }

        /// <summary>
        /// File name (without extension) of a dictionary under Themes/Schemes.
        /// </summary>
        public string SchemeName
        {
            get { return _schemeName; }

            set
            {
                if (string.IsNullOrEmpty(value) || _schemeName == value)
                {
                    return;
                }

                _schemeName = value;
                ApplyScheme();
            }
        }

        public void Initialize(ResourceDictionary appResources)
        {
            _appResources = appResources;
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
            Apply();
        }

        private void OnSystemParametersChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HighContrast")
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_appResources == null)
            {
                return;
            }

            string source = "Themes/Tokens.Dark.xaml";
            if (SystemParameters.HighContrast)
            {
                source = "Themes/Tokens.HighContrast.xaml";
            }
            else if (Resolve() == AppTheme.Light)
            {
                source = "Themes/Tokens.Light.xaml";
            }

            _appResources.MergedDictionaries[ThemeSlot] =
                new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

            RaiseThemeChanged();
        }

        private void ApplyScheme()
        {
            if (_appResources == null)
            {
                return;
            }

            _appResources.MergedDictionaries[SchemeSlot] = new ResourceDictionary
            {
                Source = new Uri("Themes/Schemes/" + _schemeName + ".xaml", UriKind.Relative)
            };

            RaiseThemeChanged();
        }

        private void RaiseThemeChanged()
        {
            EventHandler handler = ThemeChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private AppTheme Resolve()
        {
            if (_theme != AppTheme.FollowSystem)
            {
                return _theme;
            }

            return SystemPreference.IsAppsUseLightTheme() ? AppTheme.Light : AppTheme.Dark;
        }
    }
}
