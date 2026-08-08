using System.Windows;

namespace BetterTerminal.Notifications
{
    /// <summary>
    /// The library's entry point: build a <see cref="ToastNotification"/> from parsed arguments and
    /// show it. This is what "load the notifications library with parameters" resolves to - a host
    /// hands over its command line and the library does the rest.
    ///
    /// It works two ways, deciding for itself which is needed:
    ///  - no WPF application yet (a bare host, or the service-launched application before any window):
    ///    it creates one and runs its own message loop until the toast closes, returning an exit code.
    ///  - a WPF application already running (the shell showing a live notice): it just shows the toast
    ///    and returns at once, leaving the host's loop to pump it.
    /// </summary>
    public static class NotificationHost
    {
        /// <summary>Shows a toast described by a command line. Returns a process exit code.</summary>
        public static int Run(string[] args)
        {
            return Show(NotificationRequest.Parse(args));
        }

        /// <summary>Shows a toast described by a request. Returns a process exit code.</summary>
        public static int Show(NotificationRequest request)
        {
            if (request == null)
            {
                return 0;
            }

            Application application = Application.Current;
            if (application == null)
            {
                Application owned = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
                return owned.Run(Build(request));
            }

            Build(request).Show();
            return 0;
        }

        private static ToastNotification Build(NotificationRequest request)
        {
            ToastNotification toast = new ToastNotification
            {
                AppName = string.IsNullOrEmpty(request.AppName) ? "BetterTerminal" : request.AppName,
                Title = request.Title,
                Message = request.Message
            };

            if (request.Duration.HasValue)
            {
                toast.Duration = request.Duration.Value;
            }

            foreach (NotificationRequest.ButtonSpec spec in request.Buttons)
            {
                ToastAction action = NotificationActions.Build(spec);
                if (action != null)
                {
                    toast.Actions.Add(action);
                }
            }

            return toast;
        }
    }
}
