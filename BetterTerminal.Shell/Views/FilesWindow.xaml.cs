using System.Windows;
using System.Windows.Controls;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Views
{
    public partial class FilesWindow : Window
    {
        public FilesWindow()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// The selected item of a tree cannot be bound, so the one thing the view knows and the
        /// view model does not is handed over here.
        /// </summary>
        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            FileExplorerViewModel model = DataContext as FileExplorerViewModel;
            if (model != null)
            {
                model.Select(e.NewValue as FileNodeViewModel);
            }
        }
    }
}
