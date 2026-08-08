using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using BetterTerminal.Updating;

namespace BetterTerminal.Service
{
    /// <summary>
    /// The named pipe the service serves so a running application learns of a staged update at once
    /// rather than on its next start. The service runs as LocalSystem in session 0 and cannot show
    /// anything on the user's desktop; this pipe is how the news crosses into the session that can.
    ///
    /// The pipe is granted to authenticated users so the interactive application can connect. One
    /// client at a time is enough - there is a single application - and a dropped client just brings
    /// the loop back to waiting for the next one.
    /// </summary>
    internal sealed class UpdateSignal : IDisposable
    {
        private readonly object _gate = new object();
        private Version _available;
        private Thread _thread;
        private volatile bool _running;

        public void SetAvailable(Version version)
        {
            lock (_gate)
            {
                _available = version;
            }
        }

        public void Start()
        {
            _running = true;
            _thread = new Thread(ServerLoop) { IsBackground = true, Name = "update-signal" };
            _thread.Start();
        }

        public void Dispose()
        {
            _running = false;

            // WaitForConnection blocks; connecting to our own pipe releases it so the loop can see
            // that it should stop.
            try
            {
                using (NamedPipeClientStream nudge =
                    new NamedPipeClientStream(".", UpdateShared.PipeName, PipeDirection.InOut))
                {
                    nudge.Connect(500);
                }
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }
        }

        private void ServerLoop()
        {
            while (_running)
            {
                try
                {
                    using (NamedPipeServerStream server = Create())
                    {
                        server.WaitForConnection();
                        if (_running)
                        {
                            Serve(server);
                        }
                    }
                }
                catch (IOException)
                {
                    // The client vanished mid-exchange; wait for the next one.
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static NamedPipeServerStream Create()
        {
            PipeSecurity security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

            return new NamedPipeServerStream(
                UpdateShared.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.None,
                0,
                0,
                security);
        }

        private void Serve(NamedPipeServerStream server)
        {
            StreamReader reader = new StreamReader(server);
            StreamWriter writer = new StreamWriter(server) { AutoFlush = true };

            Version available;
            lock (_gate)
            {
                available = _available;
            }

            if (available != null)
            {
                writer.WriteLine(UpdateShared.UpdateMessagePrefix + UpdateShared.NormalizedString(available));
            }

            while (server.IsConnected && _running)
            {
                string request = reader.ReadLine();
                if (request == null)
                {
                    break;
                }

                if (request.StartsWith(UpdateShared.CheckRequest, StringComparison.OrdinalIgnoreCase))
                {
                    Version found = UpdateCheck.Run();
                    if (found != null)
                    {
                        SetAvailable(found);
                        writer.WriteLine(UpdateShared.UpdateMessagePrefix + UpdateShared.NormalizedString(found));
                    }
                    else
                    {
                        writer.WriteLine(UpdateShared.NoneReply);
                    }
                }
            }
        }
    }
}
