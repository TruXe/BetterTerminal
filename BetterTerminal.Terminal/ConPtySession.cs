using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using BetterTerminal.Interop;
using Microsoft.Win32.SafeHandles;

namespace BetterTerminal.Terminal
{
    // Backend 2: a pseudo console. Output is read on a dedicated thread and handed to the parser in
    // batches; input is queued so the UI thread never blocks on a full pipe.
    public sealed class ConPtySession : ITerminalSession
    {
        private const int ReadBufferBytes = 16384;
        private const int ThreadStopTimeoutMilliseconds = 2000;

        private readonly ProcessJob _job = new ProcessJob();
        private readonly BlockingCollection<byte[]> _pendingInput = new BlockingCollection<byte[]>();

        private SafePseudoConsoleHandle _pseudoConsole;
        private SafeKernelHandle _process;
        private FileStream _outputStream;
        private FileStream _inputStream;
        private Thread _readerThread;
        private Thread _writerThread;
        private RegisteredWaitHandle _exitRegistration;
        private ManualResetEvent _exitEvent;

        private int _exitCode;
        private volatile bool _hasExited;
        private volatile bool _disposed;

        private readonly CellGrid _grid;
        private readonly VtParser _parser;

        public ConPtySession(int columns, int rows, int scrollbackLines)
        {
            Columns = Math.Max(1, columns);
            Rows = Math.Max(1, rows);
            Title = string.Empty;

            _grid = new CellGrid(Columns, Rows, scrollbackLines);
            _parser = new VtParser(_grid);
            _parser.ResponseWriter = Write;
            _parser.TitleChanged += OnParserTitleChanged;
        }

        public CellGrid Grid
        {
            get { return _grid; }
        }

        public event EventHandler<TerminalOutputEventArgs> OutputReceived;

        public event EventHandler<TerminalTitleEventArgs> TitleChanged;

        public event EventHandler<TerminalExitEventArgs> Exited;

        public string Title { get; private set; }

        public bool IsRunning
        {
            get { return _process != null && !_hasExited; }
        }

        public int? ExitCode
        {
            get { return _hasExited ? (int?)_exitCode : null; }
        }

        public int Columns { get; private set; }

        public int Rows { get; private set; }

        public static bool IsSupported
        {
            get
            {
                OsVersionInfo version = new OsVersionInfo();
                version.OSVersionInfoSize = Marshal.SizeOf(typeof(OsVersionInfo));
                if (NativeMethods.RtlGetVersion(ref version) != 0)
                {
                    return false;
                }

                return version.MajorVersion > 10
                    || (version.MajorVersion == 10 && version.BuildNumber >= 17763);
            }
        }

        public void Start(ShellProfile shell, string workingDirectory)
        {
            if (_process != null)
            {
                throw new InvalidOperationException("This session has already been started.");
            }

            Title = shell.Name;

            SecurityAttributes attributes = new SecurityAttributes();
            attributes.Length = Marshal.SizeOf(typeof(SecurityAttributes));
            attributes.InheritHandle = false;

            SafeFileHandle inputRead;
            SafeFileHandle inputWrite;
            if (!NativeMethods.CreatePipe(out inputRead, out inputWrite, ref attributes, 0))
            {
                Win32Error.Throw("CreatePipe");
            }

            SafeFileHandle outputRead;
            SafeFileHandle outputWrite;
            if (!NativeMethods.CreatePipe(out outputRead, out outputWrite, ref attributes, 0))
            {
                inputRead.Dispose();
                inputWrite.Dispose();
                Win32Error.Throw("CreatePipe");
            }

            Win32Error.ThrowIfFailed(
                NativeMethods.CreatePseudoConsole(
                    new Coord((short)Columns, (short)Rows),
                    inputRead,
                    outputWrite,
                    0,
                    out _pseudoConsole),
                "CreatePseudoConsole");

            using (SafeProcThreadAttributeList attributeList = SafeProcThreadAttributeList.Create(1))
            {
                attributeList.SetPseudoConsole(_pseudoConsole);

                StartupInfoEx startupInfo = new StartupInfoEx();
                startupInfo.StartupInfo.cb = Marshal.SizeOf(typeof(StartupInfoEx));
                startupInfo.lpAttributeList = attributeList.DangerousGetHandle();

                ProcessInformation processInformation;
                if (!NativeMethods.CreateProcess(
                        null,
                        new StringBuilder(shell.BuildCommandLine()),
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                        IntPtr.Zero,
                        string.IsNullOrEmpty(workingDirectory)
                            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                            : workingDirectory,
                        ref startupInfo,
                        out processInformation))
                {
                    inputRead.Dispose();
                    inputWrite.Dispose();
                    outputRead.Dispose();
                    outputWrite.Dispose();
                    _pseudoConsole.Dispose();
                    _pseudoConsole = null;
                    Win32Error.Throw("CreateProcess");
                }

                _process = new SafeKernelHandle(processInformation.hProcess, true);
                new SafeKernelHandle(processInformation.hThread, true).Dispose();
                _job.Assign(processInformation.hProcess);
            }

            // The pseudo console owns duplicates of these ends; keeping ours open would stop the
            // reader from ever seeing end of stream.
            inputRead.Dispose();
            outputWrite.Dispose();

            _outputStream = new FileStream(outputRead, FileAccess.Read, ReadBufferBytes, false);
            _inputStream = new FileStream(inputWrite, FileAccess.Write, 4096, false);

            _readerThread = new Thread(ReadOutput);
            _readerThread.IsBackground = true;
            _readerThread.Name = "BetterTerminal output";
            _readerThread.Start();

            _writerThread = new Thread(WriteInput);
            _writerThread.IsBackground = true;
            _writerThread.Name = "BetterTerminal input";
            _writerThread.Start();

            _exitEvent = new ManualResetEvent(false);
            _exitEvent.SafeWaitHandle = new SafeWaitHandle(_process.DangerousGetHandle(), false);
            _exitRegistration = ThreadPool.RegisterWaitForSingleObject(
                _exitEvent,
                OnProcessExited,
                null,
                Timeout.Infinite,
                true);
        }

        public void Write(string text)
        {
            if (_disposed || string.IsNullOrEmpty(text) || _pendingInput.IsAddingCompleted)
            {
                return;
            }

            _pendingInput.Add(Encoding.UTF8.GetBytes(text));
        }

        public void Resize(int columns, int rows)
        {
            columns = Math.Max(1, columns);
            rows = Math.Max(1, rows);
            if (_disposed || (columns == Columns && rows == Rows))
            {
                return;
            }

            Columns = columns;
            Rows = rows;

            lock (_grid.SyncRoot)
            {
                _grid.Resize(columns, rows);
            }

            if (_pseudoConsole == null || _pseudoConsole.IsInvalid || _hasExited)
            {
                return;
            }

            Win32Error.ThrowIfFailed(
                NativeMethods.ResizePseudoConsole(_pseudoConsole, new Coord((short)columns, (short)rows)),
                "ResizePseudoConsole");
        }

        public void Close()
        {
            if (_process == null || _hasExited)
            {
                return;
            }

            // Closing the pseudo console asks the client to exit; the job guarantees the teardown.
            if (_pseudoConsole != null)
            {
                _pseudoConsole.Dispose();
                _pseudoConsole = null;
            }

            _job.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_pendingInput.IsAddingCompleted)
            {
                _pendingInput.CompleteAdding();
            }

            if (_exitRegistration != null)
            {
                _exitRegistration.Unregister(null);
                _exitRegistration = null;
            }

            // Closing the pseudo console and the job ends the client, which breaks the output pipe
            // and lets the reader thread fall out of its blocking read.
            if (_pseudoConsole != null)
            {
                _pseudoConsole.Dispose();
                _pseudoConsole = null;
            }

            _job.Dispose();

            bool writerStopped = _writerThread == null || _writerThread.Join(ThreadStopTimeoutMilliseconds);
            bool readerStopped = _readerThread == null || _readerThread.Join(ThreadStopTimeoutMilliseconds);

            // A stream or queue torn down under a still-blocked thread throws there, off any catch
            // the shell could reach, so anything still in use is left to its finalizer instead.
            if (writerStopped && _inputStream != null)
            {
                _inputStream.Dispose();
                _inputStream = null;
            }

            if (readerStopped && _outputStream != null)
            {
                _outputStream.Dispose();
                _outputStream = null;
            }

            if (_exitEvent != null)
            {
                _exitEvent.Dispose();
                _exitEvent = null;
            }

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }

            if (writerStopped)
            {
                _pendingInput.Dispose();
            }
        }

        private void ReadOutput()
        {
            byte[] bytes = new byte[ReadBufferBytes];
            char[] chars = new char[ReadBufferBytes];
            Decoder decoder = new UTF8Encoding(false).GetDecoder();
            FileStream output = _outputStream;

            try
            {
                while (true)
                {
                    int read = output.Read(bytes, 0, bytes.Length);
                    if (read <= 0)
                    {
                        return;
                    }

                    int decoded = decoder.GetChars(bytes, 0, read, chars, 0);
                    if (decoded == 0)
                    {
                        continue;
                    }

                    _parser.Parse(chars, decoded);

                    EventHandler<TerminalOutputEventArgs> handler = OutputReceived;
                    if (handler != null)
                    {
                        handler(this, new TerminalOutputEventArgs(chars, decoded));
                    }
                }
            }
            catch (IOException)
            {
                // The pipe breaks when the client exits; the process wait reports the exit code.
            }
            catch (Exception exception)
            {
                ReportFailure("Reading terminal output failed: " + exception.Message);
            }
        }

        private void WriteInput()
        {
            FileStream input = _inputStream;

            try
            {
                foreach (byte[] chunk in _pendingInput.GetConsumingEnumerable())
                {
                    input.Write(chunk, 0, chunk.Length);
                    input.Flush();
                }
            }
            catch (IOException)
            {
                // The client is gone; queued keystrokes have nowhere to go.
            }
            catch (Exception exception)
            {
                ReportFailure("Writing terminal input failed: " + exception.Message);
            }
        }

        // An exception on an IO thread would otherwise terminate the whole shell, taking every other
        // pane with it, so the failure is turned into an exit the pane can display.
        private void ReportFailure(string reason)
        {
            if (_disposed || _hasExited)
            {
                return;
            }

            _hasExited = true;
            _exitCode = -1;

            EventHandler<TerminalExitEventArgs> handler = Exited;
            if (handler != null)
            {
                handler(this, new TerminalExitEventArgs(-1, reason));
            }
        }

        private void OnProcessExited(object state, bool timedOut)
        {
            if (_hasExited || _process == null)
            {
                return;
            }

            int code;
            if (!NativeMethods.GetExitCodeProcess(_process, out code))
            {
                code = -1;
            }

            _hasExited = true;
            _exitCode = code;

            EventHandler<TerminalExitEventArgs> handler = Exited;
            if (handler != null)
            {
                handler(this, new TerminalExitEventArgs(code, "The shell process exited."));
            }
        }

        private void OnParserTitleChanged(object sender, TerminalTitleEventArgs e)
        {
            Title = e.Title;

            EventHandler<TerminalTitleEventArgs> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
