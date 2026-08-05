using System;
using System.Runtime.InteropServices;
using BetterTerminal.Interop;

namespace BetterTerminal.Terminal
{
    // Every launched shell is assigned to a job with KILL_ON_JOB_CLOSE so that closing a pane,
    // or the application dying, cannot leave an orphaned shell or conhost behind.
    public sealed class ProcessJob : IDisposable
    {
        private readonly SafeKernelHandle _job;

        public ProcessJob()
        {
            _job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (_job.IsInvalid)
            {
                Win32Error.Throw("CreateJobObject");
            }

            JobObjectExtendedLimitInformation limits = new JobObjectExtendedLimitInformation();
            limits.BasicLimitInformation.LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            if (!NativeMethods.SetInformationJobObject(
                    _job,
                    NativeMethods.JobObjectExtendedLimitInformation,
                    ref limits,
                    Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation))))
            {
                _job.Dispose();
                Win32Error.Throw("SetInformationJobObject");
            }
        }

        public void Assign(IntPtr processHandle)
        {
            if (!NativeMethods.AssignProcessToJobObject(_job, processHandle))
            {
                Win32Error.Throw("AssignProcessToJobObject");
            }
        }

        public void Dispose()
        {
            _job.Dispose();
        }
    }
}
