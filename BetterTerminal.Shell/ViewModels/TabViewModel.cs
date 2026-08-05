using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BetterTerminal.Shell.ViewModels
{
    public class TabViewModel : ObservableObject
    {
        private string _title;
        private string _fullTitle;
        private bool _hasFailed;
        private string _lastExitDescription;
        private object _rootPane;
        private PaneViewModel _focusedPane;

        public string Title
        {
            get { return _title; }
            set { Set(ref _title, value); }
        }

        public string FullTitle
        {
            get { return _fullTitle; }
            set { Set(ref _fullTitle, value); }
        }

        public bool HasFailed
        {
            get { return _hasFailed; }

            set
            {
                if (Set(ref _hasFailed, value))
                {
                    Raise("IconBrush");
                }
            }
        }

        public string LastExitDescription
        {
            get { return _lastExitDescription; }
            set { Set(ref _lastExitDescription, value); }
        }

        public object RootPane
        {
            get { return _rootPane; }
            set { Set(ref _rootPane, value); }
        }

        public PaneViewModel FocusedPane
        {
            get { return _focusedPane; }
            set { Set(ref _focusedPane, value); }
        }

        public ICommand CloseCommand { get; set; }

        public Brush IconBrush
        {
            get { return Themed(HasFailed ? "Bt.StatusErrorBrush" : "Bt.TextFillSecondaryBrush"); }
        }

        internal static Brush Themed(string key)
        {
            if (Application.Current == null)
            {
                return Brushes.Transparent;
            }

            return (Brush)Application.Current.TryFindResource(key);
        }

        public void RefreshBrushes()
        {
            Raise("IconBrush");
        }
    }
}
