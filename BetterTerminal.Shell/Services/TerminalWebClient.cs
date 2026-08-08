using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// One browser connection. It mirrors the focused session's cell grid to the page and writes the
    /// page's keystrokes back into the session - the same grid the desktop draws and the same input
    /// pipe it types into, so the two views are one terminal. The grid is polled rather than pushed:
    /// a fixed, cheap tick on localhost sidesteps the races of hooking a session that can be swapped
    /// under it when the focus moves.
    /// </summary>
    internal sealed class TerminalWebClient
    {
        // The Campbell scheme defaults, sent so a cell that carries no colour of its own is drawn the
        // way the desktop draws it.
        private const int DefaultForeground = 0x00CCCCCC;
        private const int DefaultBackground = 0x000C0C0C;

        private const int FrameDelayMs = 40;

        private readonly WebSocket _socket;
        private readonly Func<ConPtySession> _session;

        private ConPtySession _current;
        private int _cols;
        private int _rows;
        private long[] _sent;
        private int _lastCursorColumn = -1;
        private int _lastCursorRow = -1;
        private bool _lastCursorOn;

        public TerminalWebClient(WebSocket socket, Func<ConPtySession> session)
        {
            _socket = socket;
            _session = session;
        }

        public async Task RunAsync()
        {
            Task receive = ReceiveLoop();
            await SendLoop();
            await receive;
        }

        private async Task SendLoop()
        {
            while (_socket.State == WebSocketState.Open)
            {
                string init;
                string frame;
                BuildMessages(out init, out frame);

                try
                {
                    if (init != null)
                    {
                        await Send(init);
                    }

                    if (frame != null)
                    {
                        await Send(frame);
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await Task.Delay(FrameDelayMs);
            }
        }

        // The two messages a tick may produce: an init when the session or its size changed, and a
        // frame carrying the rows that changed. Each is its own WebSocket message; the browser reads
        // one JSON object per message.
        private void BuildMessages(out string init, out string frame)
        {
            init = null;
            frame = null;

            ConPtySession session = _session();
            if (session == null)
            {
                return;
            }

            CellGrid grid = session.Grid;

            lock (grid.SyncRoot)
            {
                int cols = grid.Columns;
                int rows = grid.Rows;

                if (!ReferenceEquals(session, _current) || cols != _cols || rows != _rows)
                {
                    _current = session;
                    _cols = cols;
                    _rows = rows;
                    _sent = new long[rows];
                    for (int i = 0; i < rows; i++)
                    {
                        _sent[i] = -1;
                    }

                    _lastCursorColumn = -1;
                    init = "{\"t\":\"i\",\"cols\":" + cols + ",\"rows\":" + rows +
                        ",\"fg\":" + DefaultForeground + ",\"bg\":" + DefaultBackground + "}";
                }

                int top = Math.Max(0, grid.TotalLines - rows);

                StringBuilder builder = new StringBuilder();
                builder.Append("{\"t\":\"f\",\"top\":").Append(top);
                builder.Append(",\"cx\":").Append(grid.CursorColumn);
                builder.Append(",\"cy\":").Append(grid.CursorRow);
                builder.Append(",\"cur\":").Append(grid.CursorVisible ? 1 : 0);
                builder.Append(",\"rows\":[");

                bool changed = false;
                bool firstRow = true;
                for (int row = 0; row < rows; row++)
                {
                    TerminalCell[] cells;
                    long version;
                    if (!grid.TryGetLine(top + row, out cells, out version))
                    {
                        continue;
                    }

                    if (version == _sent[row])
                    {
                        continue;
                    }

                    _sent[row] = version;
                    changed = true;

                    if (!firstRow)
                    {
                        builder.Append(',');
                    }

                    firstRow = false;
                    AppendRow(builder, row, cells, cols);
                }

                builder.Append("]}");

                bool cursorMoved = grid.CursorColumn != _lastCursorColumn
                    || grid.CursorRow != _lastCursorRow
                    || grid.CursorVisible != _lastCursorOn;
                _lastCursorColumn = grid.CursorColumn;
                _lastCursorRow = grid.CursorRow;
                _lastCursorOn = grid.CursorVisible;

                if (init != null || changed || cursorMoved)
                {
                    frame = builder.ToString();
                }
            }
        }

        private static void AppendRow(StringBuilder builder, int row, TerminalCell[] cells, int cols)
        {
            builder.Append("{\"y\":").Append(row).Append(",\"ch\":[");
            for (int c = 0; c < cols; c++)
            {
                if (c > 0)
                {
                    builder.Append(',');
                }

                char ch = c < cells.Length ? cells[c].Character : ' ';
                builder.Append((int)(ch == '\0' ? ' ' : ch));
            }

            builder.Append("],\"f\":[");
            for (int c = 0; c < cols; c++)
            {
                if (c > 0)
                {
                    builder.Append(',');
                }

                builder.Append(c < cells.Length ? cells[c].Foreground : 0);
            }

            builder.Append("],\"b\":[");
            for (int c = 0; c < cols; c++)
            {
                if (c > 0)
                {
                    builder.Append(',');
                }

                builder.Append(c < cells.Length ? cells[c].Background : 0);
            }

            builder.Append("]}");
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[8192];
            StringBuilder message = new StringBuilder();
            int emptyReads = 0;

            while (_socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // A run of empty reads means the peer is gone but the state has not flipped yet;
                // without this the loop spins and burns a core.
                if (result.Count == 0 && !result.EndOfMessage)
                {
                    if (++emptyReads > 100)
                    {
                        break;
                    }

                    continue;
                }

                emptyReads = 0;
                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                Handle(message.ToString());
                message.Clear();
            }
        }

        private void Handle(string message)
        {
            if (message.Length < 1)
            {
                return;
            }

            ConPtySession session = _session();
            if (session == null)
            {
                return;
            }

            char kind = message[0];
            string body = message.Substring(1);

            if (kind == 'x')
            {
                session.Write(body);
                return;
            }

            if (kind == 'k')
            {
                string sequence = Encode(body, session.Grid);
                if (sequence != null)
                {
                    session.Write(sequence);
                }
            }
        }

        private static string Encode(string keyName, CellGrid grid)
        {
            bool applicationCursor;
            lock (grid.SyncRoot)
            {
                applicationCursor = grid.ApplicationCursorKeys;
            }

            Key key;
            switch (keyName)
            {
                case "Enter": key = Key.Enter; break;
                case "Backspace": key = Key.Back; break;
                case "Tab": key = Key.Tab; break;
                case "Escape": key = Key.Escape; break;
                case "ArrowUp": key = Key.Up; break;
                case "ArrowDown": key = Key.Down; break;
                case "ArrowLeft": key = Key.Left; break;
                case "ArrowRight": key = Key.Right; break;
                case "Home": key = Key.Home; break;
                case "End": key = Key.End; break;
                case "PageUp": key = Key.PageUp; break;
                case "PageDown": key = Key.Next; break;
                case "Delete": key = Key.Delete; break;
                case "Insert": key = Key.Insert; break;
                default: return null;
            }

            string sequence = VtKeyEncoder.Encode(key, ModifierKeys.None, applicationCursor);
            if (sequence != null)
            {
                return sequence;
            }

            // A couple of keys the encoder may leave to the text path on the desktop still need a
            // byte here, because a browser sends no character for them.
            switch (key)
            {
                case Key.Enter: return "\r";
                case Key.Back: return "\x7f";
                case Key.Tab: return "\t";
                case Key.Escape: return "\x1b";
                default: return null;
            }
        }

        private Task Send(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            return _socket.SendAsync(
                new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
