using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// Self-populating so the designer surface shows real content (BP-R6). d:DesignData does
    /// not work on modern targets, so sample data is constructed here behind a design-mode check.
    /// At run time TerminalWorkspace fills the collections and assigns the commands.
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        private TabViewModel _selectedTab;
        private ProfileViewModel _defaultProfile;
        private string _projectName;

        public MainViewModel()
        {
            Tabs = new ObservableCollection<TabViewModel>();
            Profiles = new ObservableCollection<ProfileViewModel>();

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                SampleData.Populate(this);
            }
        }

        public ObservableCollection<TabViewModel> Tabs { get; private set; }

        public ObservableCollection<ProfileViewModel> Profiles { get; private set; }

        public TabViewModel SelectedTab
        {
            get { return _selectedTab; }

            set
            {
                if (Set(ref _selectedTab, value))
                {
                    Raise("ActivePane");
                }
            }
        }

        public ProfileViewModel DefaultProfile
        {
            get { return _defaultProfile; }
            set { Set(ref _defaultProfile, value); }
        }

        public PaneViewModel ActivePane
        {
            get { return _selectedTab == null ? null : _selectedTab.FocusedPane; }
        }

        public ICommand NewTabCommand { get; set; }

        public ICommand SplitRightCommand { get; set; }

        public ICommand SplitDownCommand { get; set; }

        public ICommand ClosePaneCommand { get; set; }

        public ICommand OpenPaletteCommand { get; set; }

        public ICommand OpenSettingsCommand { get; set; }

        public ICommand OpenProfileFlyoutCommand { get; set; }

        public ICommand FocusNextPaneCommand { get; set; }

        public ICommand FocusPreviousPaneCommand { get; set; }

        public ICommand OpenConnectionsCommand { get; set; }

        public ICommand OpenWorkspaceSetupCommand { get; set; }

        /// <summary>
        /// The project the shell was opened in, or empty for a plain launch. Shown in the status
        /// strip so it is always clear which folder the settings belong to.
        /// </summary>
        public string ProjectName
        {
            get { return _projectName; }

            set
            {
                if (Set(ref _projectName, value))
                {
                    Raise("HasProject");
                }
            }
        }

        public bool HasProject
        {
            get { return !string.IsNullOrEmpty(_projectName); }
        }

        public void RaiseActivePaneChanged()
        {
            Raise("ActivePane");
        }
    }
}
