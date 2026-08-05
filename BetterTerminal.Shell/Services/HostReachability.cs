using System;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Threading;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Answers one question about a saved connection: does the host accept a connection on the
    /// remote shell port right now. It opens a socket and closes it again - it sends nothing,
    /// reads nothing and stores nothing.
    /// </summary>
    public static class HostReachability
    {
        public const int RemoteShellPort = 22;

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Probes on a pool thread and reports back on the given dispatcher. The callback runs
        /// exactly once; a failure of any kind is reported as "not reachable", never thrown -
        /// this runs on a background thread, where an escaping exception would end the process.
        /// </summary>
        public static void Probe(Dispatcher dispatcher, string host, Action<bool> completed)
        {
            if (dispatcher == null || completed == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(host))
            {
                completed(false);
                return;
            }

            string target = host;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool reachable = TryConnect(target);

                try
                {
                    dispatcher.BeginInvoke(new Action(delegate { completed(reachable); }));
                }
                catch (InvalidOperationException)
                {
                    // The window closed while the probe was in flight.
                }
            });
        }

        private static bool TryConnect(string host)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult pending = client.BeginConnect(host, RemoteShellPort, null, null);
                    if (!pending.AsyncWaitHandle.WaitOne(Timeout))
                    {
                        return false;
                    }

                    client.EndConnect(pending);
                    return true;
                }
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
