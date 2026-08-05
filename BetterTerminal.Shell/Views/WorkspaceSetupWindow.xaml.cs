using System.Windows;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// View-only: the two buttons close the dialog with an answer, and the workspace decides what
    /// to do with the view model afterwards.
    /// </summary>
    public partial class WorkspaceSetupWindow : Window
    {
        public WorkspaceSetupWindow()
        {
            InitializeComponent();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
