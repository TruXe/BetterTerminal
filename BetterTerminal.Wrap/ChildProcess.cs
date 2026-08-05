using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using BetterTerminal.Terminal;

namespace BetterTerminal.Wrap
{
    /// <summary>
    /// Starts one script and reports what it did. Two modes, chosen by the script itself:
    /// streamed, where both pipes are drained on their own threads and the lines land in the log;
    /// and pass-through, where the child inherits this console and draws on it directly.
    ///
    /// Every child is assigned to a job object with kill-on-close, so cancelling or dying takes
    /// the whole child tree with it rather than leaving a shell running with nothing attached.
    /// </summary>
    public sealed class ChildProcess : IDisposable
    {
        private readonly RunRequest _request;
        private readonly string _workingDirectory;

        private Process _process;
        private ProcessJob _job;
        private bool _cancelling;

        public ChildProcess(RunRequest request, string workingDirectory)
        {
            _request = request;
            _workingDirectory = workingDirectory;
        }

        public bool HasExited
        {
            get { return _process == null || _process.HasExited; }
        }

        public int ExitCode
        {
            get { return _process == null ? -1 : _process.ExitCode; }
        }

        /// <summary>
        /// Starts the child with both pipes redirected and returns at once. Draining only one of
        /// them deadlocks as soon as the other fills its buffer, so both are read, and both are
        /// read by the framework's own threads rather than by the UI thread.
        /// </summary>
        public void Start(OutputLog log)
        {
            ProcessStartInfo start = Describe();
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = ChildEncoding();
            start.StandardErrorEncoding = ChildEncoding();

            _job = new ProcessJob();
            _process = new Process();
            _process.StartInfo = start;
            _process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    log.Append(e.Data);
                }
            };
            _process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    log.Append(e.Data);
                }
            };

            _process.Start();
            _job.Assign(_process.Handle);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// <summary>
        /// Runs the child on this console with nothing redirected and blocks until it exits. The
        /// caller has already put its own screen away; a remote shell or an editor needs a real
        /// console for its prompts and its own full-screen drawing, and a script that starts a
        /// shell of its own needs standard handles that are not pipes.
        /// </summary>
        public int RunOnConsole()
        {
            ProcessStartInfo start = Describe();

            _job = new ProcessJob();
            _process = Process.Start(start);
            _job.Assign(_process.Handle);
            _process.WaitForExit();
            return _process.ExitCode;
        }

        /// <summary>
        /// Asks the child to stop. It shares this console, so the interrupt has already reached it
        /// through the console itself; this waits for it to act on that and closes the job when it
        /// does not. The wait runs off the UI thread because it is allowed to take seconds.
        /// </summary>
        public void Cancel()
        {
            if (_process == null || _cancelling)
            {
                return;
            }

            _cancelling = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Process child = _process;
                ProcessJob job = _job;

                try
                {
                    if (child != null && !child.WaitForExit(2000) && job != null)
                    {
                        job.Dispose();
                    }
                }
                catch (InvalidOperationException)
                {
                    // The child had already gone by the time the wait started.
                }
            });
        }

        /// <summary>
        /// Waits for the redirected readers to finish after the child exits. The parameterless
        /// wait is what flushes the last lines out of the pipes; the timed overload does not.
        /// </summary>
        public void WaitForOutput()
        {
            if (_process != null)
            {
                _process.WaitForExit();
            }
        }

        public void Dispose()
        {
            if (_job != null)
            {
                _job.Dispose();
                _job = null;
            }

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }
        }

        private ProcessStartInfo Describe()
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.UseShellExecute = false;
            start.CreateNoWindow = false;
            start.WorkingDirectory = _workingDirectory;

            string arguments = _request.BuildArguments();
            string extension = Path.GetExtension(_request.ScriptPath);

            if (string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                start.FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell\\v1.0\\powershell.exe");

                // -File is what makes the script's own exit code the process exit code; -Command
                // reports the success of the command it was handed, not of the script.
                start.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + _request.ScriptPath + "\""
                    + (arguments.Length == 0 ? string.Empty : " " + arguments);
                return start;
            }

            start.FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

            // /s makes cmd strip exactly the outer pair of quotes and take the rest literally,
            // which is the only reliable way to pass a quoted path plus quoted arguments.
            start.Arguments = "/s /c \"\"" + _request.ScriptPath + "\""
                + (arguments.Length == 0 ? string.Empty : " " + arguments) + "\"";
            return start;
        }

        /// <summary>
        /// A child writes its redirected output in the console's output code page, which it
        /// inherits from this process - and this process set that to UTF-8 when it took the
        /// console over. Assuming the machine's OEM page instead put a bar where the accent in
        /// "Terminál" belongs in the first script error this program ever showed.
        /// </summary>
        private static Encoding ChildEncoding()
        {
            return Console.OutputEncoding;
        }
    }
}
