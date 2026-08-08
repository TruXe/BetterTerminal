using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Serves the focused terminal to a browser on this machine. It binds to 127.0.0.1 only, so it
    /// is reachable from this computer and nowhere else - a terminal is shell access, and putting
    /// that on the network without a gate would hand a shell to anyone who could reach the port. A
    /// phone reaches it through a tunnel the user sets up themselves, which keeps the gate theirs.
    ///
    /// Binding to a concrete address rather than a wildcard is also what lets it start without an
    /// administrator URL reservation.
    /// </summary>
    public sealed class WebServer
    {
        private readonly int _port;
        private readonly Func<ConPtySession> _session;
        private HttpListener _listener;
        private volatile bool _running;

        public WebServer(int port, Func<ConPtySession> session)
        {
            _port = port;
            _session = session;
        }

        public int Port
        {
            get { return _port; }
        }

        public string Url
        {
            get { return "http://127.0.0.1:" + _port + "/"; }
        }

        public bool TryStart(out string error)
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _running = true;
                Task.Run((Func<Task>)AcceptLoop);
                error = null;
                return true;
            }
            catch (HttpListenerException ex)
            {
                // The usual cause is the port already being in use.
                error = ex.Message;
                _listener = null;
                return false;
            }
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _listener = null;
            }
        }

        private async Task AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    // The listener was stopped; leave the loop.
                    break;
                }

                // Each connection is handled on its own; the accept loop must not wait on it.
                Task ignored = Task.Run(delegate { return Handle(context); });
                GC.KeepAlive(ignored);
            }
        }

        private async Task Handle(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath;

                if (context.Request.IsWebSocketRequest)
                {
                    if (path != "/ws")
                    {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                        return;
                    }

                    HttpListenerWebSocketContext socket = await context.AcceptWebSocketAsync(null);
                    await new TerminalWebClient(socket.WebSocket, _session).RunAsync();
                    return;
                }

                ServeStatic(context, path);
            }
            catch (Exception)
            {
                try
                {
                    context.Response.Abort();
                }
                catch (Exception)
                {
                }
            }
        }

        private static void ServeStatic(HttpListenerContext context, string path)
        {
            string body;
            string type;

            switch (path)
            {
                case "/":
                case "/index.html":
                    body = WebAssets.Html;
                    type = "text/html; charset=utf-8";
                    break;
                case "/app.css":
                    body = WebAssets.Css;
                    type = "text/css; charset=utf-8";
                    break;
                case "/app.js":
                    body = WebAssets.Js;
                    type = "application/javascript; charset=utf-8";
                    break;
                default:
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = type;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }
    }
}
