using System.Windows;

namespace BetterTerminal.Shell.Views
{
    public partial class ConnectionsWindow : Window
    {
        public ConnectionsWindow()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
