using System.Windows.Input;
using System.Windows.Media;
using BetterTerminal.Shell.Views;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>A leaf pane: one terminal session plus the state its header shows.</summary>
    public class PaneViewModel : ObservableObject
    {
        private bool _isFocused;
        private bool _isDropTarget;
        private string _workingDirectory;
        private string _shellDescription;
        private string _badgeText;
        private int _lastExitCode;
        private bool _hasExited;
        private string _sessionTitle;

        public PaneViewModel()
        {
        }

        public PaneViewModel(TerminalSurface surface)
        {
            Surface = surface;
            _workingDirectory = surface.WorkingDirectory;
            _shellDescription = surface.Shell.Name;
            _badgeText = surface.Shell.Name;

            surface.TitleChanged += OnSurfaceTitleChanged;
            surface.Exited += OnSurfaceExited;
        }

        /// <summary>The live terminal, hosted by the pane template. Null in design-time data.</summary>
        public TerminalSurface Surface { get; private set; }

        public bool IsFocused
        {
            get { return _isFocused; }

            set
            {
                if (Set(ref _isFocused, value))
                {
                    Raise("FocusDotBrush");
                }
            }
        }

        public bool IsDropTarget
        {
            get { return _isDropTarget; }
            set { Set(ref _isDropTarget, value); }
        }

        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set { Set(ref _workingDirectory, value); }
        }

        public string ShellDescription
        {
            get { return _shellDescription; }
            set { Set(ref _shellDescription, value); }
        }

        public int LastExitCode
        {
            get { return _lastExitCode; }

            set
            {
                if (Set(ref _lastExitCode, value))
                {
                    Raise("BadgeBrush");
                    Raise("ExitBrush");
                    Raise("ExitDescription");
                }
            }
        }

        public string DesignTimePreview { get; set; }

        public bool HasBadge
        {
            get { return !string.IsNullOrEmpty(BadgeText); }
        }

        public string BadgeText
        {
            get { return _badgeText; }

            set
            {
                if (Set(ref _badgeText, value))
                {
                    Raise("HasBadge");
                }
            }
        }

        public Brush BadgeBrush
        {
            get { return TabViewModel.Themed(_hasExited && _lastExitCode != 0 ? "Bt.StatusErrorBrush" : "Bt.AccentTextBrush"); }
        }

        public Brush FocusDotBrush
        {
            get { return TabViewModel.Themed(IsFocused ? "Bt.AccentFillDefaultBrush" : "Bt.StrokeDefaultBrush"); }
        }

        public Brush ExitBrush
        {
            get
            {
                if (!_hasExited)
                {
                    return TabViewModel.Themed("Bt.StatusSuccessBrush");
                }

                return TabViewModel.Themed(_lastExitCode == 0 ? "Bt.StatusSuccessBrush" : "Bt.StatusErrorBrush");
            }
        }

        public string ExitDescription
        {
            get { return _hasExited ? "exit " + _lastExitCode : "running"; }
        }

        public ICommand CloseCommand { get; set; }

        /// <summary>Re-reads every themed brush after the theme or scheme slot changed.</summary>
        public void RefreshBrushes()
        {
            Raise("BadgeBrush");
            Raise("FocusDotBrush");
            Raise("ExitBrush");
        }

        /// <summary>
        /// What the shell reports as its window title. Kept apart from WorkingDirectory, which
        /// stays the directory the session was started in: cmd.exe reports its own image path as
        /// the title, so using it as the path would put a lie in the pane header.
        /// </summary>
        public string SessionTitle
        {
            get { return _sessionTitle; }
            set { Set(ref _sessionTitle, value); }
        }

        private void OnSurfaceTitleChanged(object sender, TerminalTitleEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Title))
            {
                SessionTitle = e.Title;
            }
        }

        private void OnSurfaceExited(object sender, TerminalExitEventArgs e)
        {
            _hasExited = true;
            LastExitCode = e.ExitCode;
            BadgeText = "exit " + e.ExitCode;
        }
    }
}
