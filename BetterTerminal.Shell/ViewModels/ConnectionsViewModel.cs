using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// The saved connections view: the list, the two fields that add to it, and where a chosen
    /// connection should open. It keeps no files and starts no sessions itself - it raises the
    /// three events below and the workspace does that work.
    /// </summary>
    public class ConnectionsViewModel : ObservableObject
    {
        private ConnectionViewModel _selected;
        private string _newUserName;
        private string _newHost;
        private string _message;
        private bool _opensInSeparateWindow;

        public ConnectionsViewModel()
        {
            Connections = new ObservableCollection<ConnectionViewModel>();
            SaveCommand = new ShellCommand(Add);
            RefreshCommand = new ShellCommand(Refresh);
            ConnectCommand = new ShellCommand(Connect);
            _message = "Add a connection, or pick one and connect.";
        }

        /// <summary>The list changed and should be written back to disk.</summary>
        public event EventHandler Changed;

        /// <summary>Every connection should be checked again.</summary>
        public event EventHandler RefreshRequested;

        /// <summary>A connection was chosen; the argument carries which one and where.</summary>
        public event EventHandler<ConnectRequestedEventArgs> ConnectRequested;

        public ObservableCollection<ConnectionViewModel> Connections { get; private set; }

        public ConnectionViewModel Selected
        {
            get { return _selected; }
            set { Set(ref _selected, value); }
        }

        public string NewUserName
        {
            get { return _newUserName; }
            set { Set(ref _newUserName, value); }
        }

        public string NewHost
        {
            get { return _newHost; }
            set { Set(ref _newHost, value); }
        }

        /// <summary>What the panel says under the form: never a silent failure.</summary>
        public string Message
        {
            get { return _message; }
            set { Set(ref _message, value); }
        }

        public bool OpensInSeparateWindow
        {
            get { return _opensInSeparateWindow; }

            set
            {
                if (Set(ref _opensInSeparateWindow, value))
                {
                    Raise("OpensInGrid");
                }
            }
        }

        public bool OpensInGrid
        {
            get { return !_opensInSeparateWindow; }
            set { OpensInSeparateWindow = !value; }
        }

        public ICommand SaveCommand { get; private set; }

        public ICommand RefreshCommand { get; private set; }

        public ICommand ConnectCommand { get; private set; }

        public void Add(string userName, string host)
        {
            ConnectionViewModel connection = new ConnectionViewModel(userName, host);
            ConnectionViewModel captured = connection;
            connection.RemoveCommand = new ShellCommand(delegate { Remove(captured); });
            Connections.Add(connection);
        }

        public void Remove(ConnectionViewModel connection)
        {
            if (connection == null || !Connections.Remove(connection))
            {
                return;
            }

            Message = "Removed " + connection.Display + ".";
            RaiseChanged();
        }

        private void Add()
        {
            string userName = (NewUserName ?? string.Empty).Trim();
            string host = (NewHost ?? string.Empty).Trim();

            if (userName.Length == 0 || host.Length == 0)
            {
                Message = "Both a user name and an address are needed.";
                return;
            }

            if (userName.IndexOf(' ') >= 0 || host.IndexOf(' ') >= 0)
            {
                Message = "A user name and an address cannot contain spaces.";
                return;
            }

            foreach (ConnectionViewModel existing in Connections)
            {
                if (string.Equals(existing.UserName, userName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Host, host, StringComparison.OrdinalIgnoreCase))
                {
                    Selected = existing;
                    Message = "That connection is already saved.";
                    return;
                }
            }

            Add(userName, host);
            Selected = Connections[Connections.Count - 1];
            NewUserName = string.Empty;
            NewHost = string.Empty;
            Message = "Saved " + Selected.Display + ".";
            RaiseChanged();
            Refresh();
        }

        private void Refresh()
        {
            EventHandler handler = RefreshRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void Connect()
        {
            if (Selected == null)
            {
                Message = "Pick a connection first.";
                return;
            }

            EventHandler<ConnectRequestedEventArgs> handler = ConnectRequested;
            if (handler != null)
            {
                handler(this, new ConnectRequestedEventArgs(Selected, OpensInSeparateWindow));
            }
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

    public sealed class ConnectRequestedEventArgs : EventArgs
    {
        public ConnectRequestedEventArgs(ConnectionViewModel connection, bool separateWindow)
        {
            Connection = connection;
            SeparateWindow = separateWindow;
        }

        public ConnectionViewModel Connection { get; private set; }

        public bool SeparateWindow { get; private set; }
    }
}
