using System;
using System.Windows.Input;

namespace BetterTerminal.Shell
{
    public sealed class ShellCommand : ICommand
    {
        private readonly Action _execute;

        public ShellCommand(Action execute)
        {
            _execute = execute;
        }

        // The shell has no disabled commands; the handlers themselves ignore impossible states.
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
