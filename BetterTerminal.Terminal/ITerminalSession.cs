using System;

namespace BetterTerminal.Terminal
{
    public interface ITerminalSession : IDisposable
    {
        string Title { get; }

        bool IsRunning { get; }

        int? ExitCode { get; }

        int Columns { get; }

        int Rows { get; }

        event EventHandler<TerminalOutputEventArgs> OutputReceived;

        event EventHandler<TerminalTitleEventArgs> TitleChanged;

        event EventHandler<TerminalExitEventArgs> Exited;

        void Start(ShellProfile shell, string workingDirectory);

        void Write(string text);

        void Resize(int columns, int rows);

        void Close();
    }
}
