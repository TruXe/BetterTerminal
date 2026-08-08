using System;
using System.Diagnostics;
using System.Reflection;

namespace BetterTerminal.Notifications
{
    /// <summary>
    /// Turns a button's named action from the command line into a real <see cref="ToastAction"/>. The
    /// action names are the "functions" a button can call across the process boundary - a delegate
    /// cannot be handed to another process, so the caller names one of these and the library performs
    /// it here.
    ///
    ///   install / open   start BetterTerminal - a normal launch applies any staged build and opens
    ///                    the window
    ///   later / dismiss  just close the toast
    ///
    /// An unknown name becomes a button that only dismisses, captioned with the name itself, so a
    /// caller is never left with a dead-looking notice.
    /// </summary>
    public static class NotificationActions
    {
        public static ToastAction Build(NotificationRequest.ButtonSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Action))
            {
                return null;
            }

            string id = spec.Action.ToLowerInvariant();
            string label;
            Action<ToastNotification> invoke;

            switch (id)
            {
                case "install":
                    label = "Install now";
                    invoke = delegate { StartApplication(); };
                    break;
                case "open":
                    label = "Open";
                    invoke = delegate { StartApplication(); };
                    break;
                case "later":
                    label = "Later";
                    invoke = null;
                    break;
                case "dismiss":
                    label = "Dismiss";
                    invoke = null;
                    break;
                default:
                    // Unknown action: a plain dismiss button captioned with whatever was asked for.
                    label = spec.Action;
                    invoke = null;
                    break;
            }

            return new ToastAction(spec.Label ?? label, invoke);
        }

        /// <summary>
        /// Starts a fresh, ordinary launch of the host application. The host is BetterTerminal, so a
        /// normal start applies whatever build the service staged and then opens the window.
        /// </summary>
        private static void StartApplication()
        {
            try
            {
                string executable = HostExecutable();
                if (string.IsNullOrEmpty(executable))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Best effort: the notice was read either way, and the next manual launch still
                // applies the staged build.
            }
        }

        private static string HostExecutable()
        {
            try
            {
                Assembly entry = Assembly.GetEntryAssembly();
                if (entry != null && !string.IsNullOrEmpty(entry.Location))
                {
                    return entry.Location;
                }
            }
            catch (Exception)
            {
            }

            return Process.GetCurrentProcess().MainModule.FileName;
        }
    }
}
