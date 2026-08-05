using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BetterTerminal.Interop;

namespace BetterTerminal.Terminal
{
    // Backend 1: a real console window, reparented into the WPF shell.
    // conhost.exe is launched explicitly with the shell as its command line because on Windows 11
    // a plain CREATE_NEW_CONSOLE can be handed off to the default terminal application, which
    // leaves no classic console window to reparent.
    public sealed class HwndConsoleSession : ITerminalSession
    {
        private const string ConsoleWindowClass = "ConsoleWindowClass";
        private const int WindowSearchTimeoutMilliseconds = 5000;
        private const int WindowSearchIntervalMilliseconds = 50;

        private readonly ProcessJob _job = new ProcessJob();
        private readonly CancellationTokenSource _windowSearch = new CancellationTokenSource();

        private Process _host;
        private IntPtr _consoleWindow;
        private int _exitCode;
        private bool _hasExited;
        private bool _disposed;

        // This backend draws into its own console window, so it never produces an output stream.
        public event EventHandler<TerminalOutputEventArgs> OutputReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<TerminalTitleEventArgs> TitleChanged;

        public event EventHandler<TerminalExitEventArgs> Exited;

        public event EventHandler ConsoleWindowReady;

        public string Title { get; private set; }

        public bool IsRunning
        {
            get { return _host != null && !_hasExited; }
        }

        public int? ExitCode
        {
            get { return _hasExited ? (int?)_exitCode : null; }
        }

        public int Columns { get; private set; }

        public int Rows { get; private set; }

        public IntPtr ConsoleWindowHandle
        {
            get { return _consoleWindow; }
        }

        public void Start(ShellProfile shell, string workingDirectory)
        {
            if (_host != null)
            {
                throw new InvalidOperationException("This session has already been started.");
            }

            Title = shell.Name;

            string conhost = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "conhost.exe");

            ProcessStartInfo startInfo = new ProcessStartInfo(conhost, shell.BuildCommandLine())
            {
                UseShellExecute = false,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : workingDirectory
            };

            _host = Process.Start(startInfo);
            if (_host == null)
            {
                throw new InvalidOperationException("conhost.exe did not start for " + shell.Name + ".");
            }

            _job.Assign(_host.Handle);
            _host.EnableRaisingEvents = true;
            _host.Exited += OnHostExited;

            Task.Run(new Func<Task>(SearchConsoleWindowAsync));
        }

        public void Write(string text)
        {
            // A hosted console window owns its own input queue; there is no supported way to inject
            // text into another process's console without attaching to it. Use the ConPTY backend.
            throw new NotSupportedException("The hosted console backend does not accept programmatic input.");
        }

        public void Resize(int columns, int rows)
        {
            // The console sizes itself from its window rect, which ConsoleHwndHost drives with MoveWindow.
            Columns = columns;
            Rows = rows;
        }

        public void Close()
        {
            if (_host == null || _hasExited)
            {
                return;
            }

            _windowSearch.Cancel();
            _job.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _windowSearch.Cancel();

            if (_host != null)
            {
                _host.Exited -= OnHostExited;
            }

            _job.Dispose();
            _windowSearch.Dispose();

            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }
        }

        private async Task SearchConsoleWindowAsync()
        {
            int processId = _host.Id;
            int waited = 0;

            while (waited < WindowSearchTimeoutMilliseconds && !_windowSearch.IsCancellationRequested)
            {
                IntPtr found = FindConsoleWindow(processId);
                if (found != IntPtr.Zero)
                {
                    _consoleWindow = found;

                    StringBuilder text = new StringBuilder(512);
                    if (NativeMethods.GetWindowText(found, text, text.Capacity) > 0)
                    {
                        Title = text.ToString();
                        RaiseTitleChanged(Title);
                    }

                    EventHandler ready = ConsoleWindowReady;
                    if (ready != null)
                    {
                        ready(this, EventArgs.Empty);
                    }

                    return;
                }

                // The console window is created asynchronously by conhost; there is no wait handle for it.
                await Task.Delay(WindowSearchIntervalMilliseconds).ConfigureAwait(false);
                waited += WindowSearchIntervalMilliseconds;
            }

            if (!_windowSearch.IsCancellationRequested)
            {
                RaiseExited(-1, "The console window was not created within " +
                    (WindowSearchTimeoutMilliseconds / 1000) + " seconds.");
            }
        }

        private static IntPtr FindConsoleWindow(int processId)
        {
            IntPtr result = IntPtr.Zero;
            StringBuilder className = new StringBuilder(64);

            NativeMethods.EnumWindows(
                delegate(IntPtr hWnd, IntPtr lParam)
                {
                    int owner;
                    NativeMethods.GetWindowThreadProcessId(hWnd, out owner);
                    if (owner != processId)
                    {
                        return true;
                    }

                    className.Length = 0;
                    NativeMethods.GetClassName(hWnd, className, className.Capacity);
                    if (className.ToString() != ConsoleWindowClass)
                    {
                        return true;
                    }

                    result = hWnd;
                    return false;
                },
                IntPtr.Zero);

            return result;
        }

        private void OnHostExited(object sender, EventArgs e)
        {
            int code;
            try
            {
                code = _host.ExitCode;
            }
            catch (InvalidOperationException)
            {
                code = -1;
            }

            RaiseExited(code, "The shell process exited.");
        }

        private void RaiseExited(int exitCode, string reason)
        {
            if (_hasExited)
            {
                return;
            }

            _hasExited = true;
            _exitCode = exitCode;
            _consoleWindow = IntPtr.Zero;

            EventHandler<TerminalExitEventArgs> handler = Exited;
            if (handler != null)
            {
                handler(this, new TerminalExitEventArgs(exitCode, reason));
            }
        }

        private void RaiseTitleChanged(string title)
        {
            EventHandler<TerminalTitleEventArgs> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, new TerminalTitleEventArgs(title));
            }
        }
    }
}
