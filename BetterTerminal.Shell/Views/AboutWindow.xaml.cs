using System.Windows;

namespace BetterTerminal.Shell.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
