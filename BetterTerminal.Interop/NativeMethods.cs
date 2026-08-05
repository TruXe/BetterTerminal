using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BetterTerminal.Interop
{
    public static class NativeMethods
    {
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        public const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        public const int JobObjectExtendedLimitInformation = 9;

        public const int STD_OUTPUT_HANDLE = -11;
        public const int ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        // kernel32, processthreadsapi.h: PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, Windows 10 17763+.
        public static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreatePipe(
            out SafeFileHandle hReadPipe,
            out SafeFileHandle hWritePipe,
            ref SecurityAttributes lpPipeAttributes,
            int nSize);
        // kernel32, namedpipeapi.h CreatePipe.

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int CreatePseudoConsole(
            Coord size,
            SafeFileHandle hInput,
            SafeFileHandle hOutput,
            int dwFlags,
            out SafePseudoConsoleHandle phPC);
        // kernel32, consoleapi.h, Windows 10 17763+; returns HRESULT, S_OK is 0.

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ResizePseudoConsole(SafePseudoConsoleHandle hPC, Coord size);
        // kernel32, consoleapi.h, Windows 10 17763+; returns HRESULT, S_OK is 0.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);
        // kernel32, processthreadsapi.h InitializeProcThreadAttributeList.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            int dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);
        // kernel32, processthreadsapi.h UpdateProcThreadAttribute.

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);
        // kernel32, processthreadsapi.h DeleteProcThreadAttributeList.

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref StartupInfoEx lpStartupInfo,
            out ProcessInformation lpProcessInformation);
        // kernel32, processthreadsapi.h CreateProcessW.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(SafeKernelHandle hProcess, out int lpExitCode);
        // kernel32, processthreadsapi.h GetExitCodeProcess.

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeKernelHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);
        // kernel32, jobapi2.h CreateJobObjectW.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetInformationJobObject(
            SafeKernelHandle hJob,
            int jobObjectInformationClass,
            ref JobObjectExtendedLimitInformation lpJobObjectInformation,
            int cbJobObjectInformationLength);
        // kernel32, jobapi2.h SetInformationJobObject.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AssignProcessToJobObject(SafeKernelHandle hJob, IntPtr hProcess);
        // kernel32, jobapi2.h AssignProcessToJobObject.

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);
        // kernel32, processenv.h; the returned handle is owned by the process, never closed here.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);
        // kernel32, consoleapi.h.

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
        // kernel32, consoleapi.h; ENABLE_VIRTUAL_TERMINAL_PROCESSING is off by default in conhost.

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);
        // user32, winuser.h CreateWindowExW.

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hWnd);
        // user32, winuser.h DestroyWindow.

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        // user32, winuser.h SetParent.

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        // user32, winuser.h GetWindowLongPtrW; 64-bit only export, this solution targets x64.

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        // user32, winuser.h SetWindowLongPtrW; 64-bit only export, this solution targets x64.

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
        // user32, winuser.h SetWindowPos.

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);
        // user32, winuser.h MoveWindow.

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        // user32, winuser.h ShowWindow; documented as not setting last error.

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetFocus(IntPtr hWnd);
        // user32, winuser.h SetFocus.

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        // user32, winuser.h EnumWindows.

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        // user32, winuser.h GetWindowThreadProcessId.

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        // user32, winuser.h GetClassNameW.

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        // user32, winuser.h GetWindowTextW.

        [DllImport("ntdll.dll")]
        public static extern int RtlGetVersion(ref OsVersionInfo versionInformation);
        // ntdll, wdm.h RtlGetVersion; reports the real build number, unlike GetVersionEx and
        // Environment.OSVersion, which are shimmed for applications without an OS compatibility manifest.
        // Returns NTSTATUS, STATUS_SUCCESS is 0.
    }
}
