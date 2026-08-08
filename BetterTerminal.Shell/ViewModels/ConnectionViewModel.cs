using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>Whether the host answered on the remote shell port the last time it was asked.</summary>
    public enum ConnectionStatus
    {
        Unknown,
        Checking,
        Reachable,
        Unreachable
    }

    /// <summary>
    /// One saved connection: the two fields the user typed, plus the result of the last
    /// reachability check. The command it produces is sent to a session as typed input - it is
    /// never used to build a child process command line.
    ///
    /// The status has no glyph or brush here on purpose: the heart and its colour are a
    /// presentation decision and live in the connections view, next to the other icons.
    /// </summary>
    public class ConnectionViewModel : ObservableObject
    {
        private string _userName;
        private string _host;
        private ConnectionStatus _status;

        public ConnectionViewModel()
        {
        }

        public ConnectionViewModel(string userName, string host)
        {
            _userName = userName;
            _host = host;
        }

        public string UserName
        {
            get { return _userName; }

            set
            {
                if (Set(ref _userName, value))
                {
                    Raise("Display");
                    Raise("CommandLine");
                }
            }
        }

        public string Host
        {
            get { return _host; }

            set
            {
                if (Set(ref _host, value))
                {
                    Raise("Display");
                    Raise("CommandLine");
                }
            }
        }

        public string Display
        {
            get { return _userName + "@" + _host; }
        }

        public string CommandLine
        {
            get { return "ssh " + Display; }
        }

        public ConnectionStatus Status
        {
            get { return _status; }

            set
            {
                if (Set(ref _status, value))
                {
                    Raise("StatusDescription");
                }
            }
        }

        public string StatusDescription
        {
            get
            {
                if (_status == ConnectionStatus.Reachable)
                {
                    return "Answered on the standard port";
                }

                if (_status == ConnectionStatus.Unreachable)
                {
                    return "No answer on the standard port";
                }

                return _status == ConnectionStatus.Checking ? "Checking" : "Not checked yet";
            }
        }

        public ICommand RemoveCommand { get; set; }

        /// <summary>Opens this connection without having to select it first.</summary>
        public ICommand ConnectCommand { get; set; }
    }
}
