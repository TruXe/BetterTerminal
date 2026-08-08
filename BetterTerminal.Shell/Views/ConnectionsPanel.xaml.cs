using System.Windows.Controls;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// The saved-connection address book as a plain panel. It carries no caption and no close
    /// button, so the same instance is at home in a window or as a leaf of the pane grid.
    /// </summary>
    public partial class ConnectionsPanel : UserControl
    {
        public ConnectionsPanel()
        {
            InitializeComponent();
        }
    }
}
