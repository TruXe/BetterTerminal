using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>One command the user defined for this project.</summary>
    public class CommandEntryViewModel : ObservableObject
    {
        private string _name;
        private string _text;

        public CommandEntryViewModel()
        {
        }

        public CommandEntryViewModel(string name, string text)
        {
            _name = name;
            _text = text;
        }

        public string Name
        {
            get { return _name; }
            set { Set(ref _name, value); }
        }

        public string Text
        {
            get { return _text; }
            set { Set(ref _text, value); }
        }

        public ICommand RemoveCommand { get; set; }
    }

    /// <summary>One named value the user keeps with this project.</summary>
    public class ValueEntryViewModel : ObservableObject
    {
        private string _key;
        private string _value;

        public ValueEntryViewModel()
        {
        }

        public ValueEntryViewModel(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public string Key
        {
            get { return _key; }
            set { Set(ref _key, value); }
        }

        public string Value
        {
            get { return _value; }
            set { Set(ref _value, value); }
        }

        public ICommand RemoveCommand { get; set; }
    }

    /// <summary>
    /// The workspace setup: everything kept in the project folder. It is pure state - the window
    /// that shows it writes the result, and the workspace applies it.
    /// </summary>
    public class WorkspaceSetupViewModel : ObservableObject
    {
        private string _name;
        private string _selectedShell;
        private string _startupCommand;
        private bool _showSetupOnOpen;
        private bool _localServer;
        private string _localServerPort = DefaultPort.ToString();
        private string _newCommandName;
        private string _newCommandText;
        private string _newValueKey;
        private string _newValueText;
        private string _message;

        public WorkspaceSetupViewModel()
        {
            Shells = new ObservableCollection<string>();
            Commands = new ObservableCollection<CommandEntryViewModel>();
            Values = new ObservableCollection<ValueEntryViewModel>();
            AddCommandCommand = new ShellCommand(AddCommand);
            AddValueCommand = new ShellCommand(AddValue);
            _showSetupOnOpen = true;
            _message = "Stored in a hidden folder inside the project.";
        }

        /// <summary>The project folder itself. Read-only: it comes from where the shell started.</summary>
        public string Directory { get; set; }

        public string Name
        {
            get { return _name; }
            set { Set(ref _name, value); }
        }

        public ObservableCollection<string> Shells { get; private set; }

        public string SelectedShell
        {
            get { return _selectedShell; }
            set { Set(ref _selectedShell, value); }
        }

        public string StartupCommand
        {
            get { return _startupCommand; }
            set { Set(ref _startupCommand, value); }
        }

        public bool ShowSetupOnOpen
        {
            get { return _showSetupOnOpen; }
            set { Set(ref _showSetupOnOpen, value); }
        }

        /// <summary>The port used when the field is blank or not a number.</summary>
        public const int DefaultPort = 1100;

        /// <summary>
        /// Serve this project's terminal to a browser on this machine. It listens on localhost only,
        /// so a phone or tablet reaches it through a tunnel the user sets up - a terminal is shell
        /// access, and it is not put on the open network on a checkbox.
        /// </summary>
        public bool LocalServer
        {
            get { return _localServer; }
            set { Set(ref _localServer, value); }
        }

        public string LocalServerPort
        {
            get { return _localServerPort; }
            set { Set(ref _localServerPort, value); }
        }

        /// <summary>The chosen port, or the default when the field is blank or out of range.</summary>
        public int ResolvedPort
        {
            get
            {
                int port;
                return int.TryParse((_localServerPort ?? string.Empty).Trim(), out port)
                    && port >= 1 && port <= 65535
                    ? port
                    : DefaultPort;
            }
        }

        public ObservableCollection<CommandEntryViewModel> Commands { get; private set; }

        public ObservableCollection<ValueEntryViewModel> Values { get; private set; }

        public string NewCommandName
        {
            get { return _newCommandName; }
            set { Set(ref _newCommandName, value); }
        }

        public string NewCommandText
        {
            get { return _newCommandText; }
            set { Set(ref _newCommandText, value); }
        }

        public string NewValueKey
        {
            get { return _newValueKey; }
            set { Set(ref _newValueKey, value); }
        }

        public string NewValueText
        {
            get { return _newValueText; }
            set { Set(ref _newValueText, value); }
        }

        public string Message
        {
            get { return _message; }
            set { Set(ref _message, value); }
        }

        public ICommand AddCommandCommand { get; private set; }

        public ICommand AddValueCommand { get; private set; }

        public void AddCommand(string name, string text)
        {
            CommandEntryViewModel entry = new CommandEntryViewModel(name, text);
            CommandEntryViewModel captured = entry;
            entry.RemoveCommand = new ShellCommand(delegate { Commands.Remove(captured); });
            Commands.Add(entry);
        }

        public void AddValue(string key, string value)
        {
            ValueEntryViewModel entry = new ValueEntryViewModel(key, value);
            ValueEntryViewModel captured = entry;
            entry.RemoveCommand = new ShellCommand(delegate { Values.Remove(captured); });
            Values.Add(entry);
        }

        private void AddCommand()
        {
            string name = (NewCommandName ?? string.Empty).Trim();
            string text = (NewCommandText ?? string.Empty).Trim();

            if (name.Length == 0 || text.Length == 0)
            {
                Message = "A command needs both a name and a line to run.";
                return;
            }

            AddCommand(name, text);
            NewCommandName = string.Empty;
            NewCommandText = string.Empty;
            Message = "Added " + name + " to this project.";
        }

        private void AddValue()
        {
            string key = (NewValueKey ?? string.Empty).Trim();

            if (key.Length == 0)
            {
                Message = "A value needs a name.";
                return;
            }

            AddValue(key, NewValueText ?? string.Empty);
            NewValueKey = string.Empty;
            NewValueText = string.Empty;
            Message = "Added " + key + " to this project.";
        }
    }
}
