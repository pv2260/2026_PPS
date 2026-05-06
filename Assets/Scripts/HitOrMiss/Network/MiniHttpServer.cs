using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace HitOrMiss.Network
{
    /// <summary>
    /// Minimal self-contained HTTP/1.1 + WebSocket (RFC 6455) server built on
    /// raw <see cref="TcpListener"/>. Replaces <see cref="HttpListener"/> in
    /// Unity contexts where the Mono BCL stubs WebSocket support with
    /// <c>NotImplementedException</c>.
    ///
    /// What it implements:
    ///   * HTTP/1.1 request parsing: request-line + headers + Content-Length body.
    ///   * HTTP/1.1 response writing: status-line + headers + body.
    ///   * WebSocket upgrade handshake (Sec-WebSocket-Accept = base64(SHA1(key + GUID))).
    ///   * RFC 6455 frames: text/binary/close/ping/pong, masked client→server,
    ///     unmasked server→client, payload lengths up to int.MaxValue.
    ///
    /// What it does NOT implement (intentionally — the clinician console
    /// doesn't need it):
    ///   * Keep-alive / pipelined HTTP requests on the same connection.
    ///   * Chunked transfer encoding (request side).
    ///   * Compression / permessage-deflate WebSocket extension.
    ///   * TLS / wss:// (use a reverse proxy or add a trusted cert if you
    ///     ever need it; lab LAN use case doesn't require it).
    ///
    /// Threading: connection accept loop runs on a background task; each
    /// connection's handler runs on its own task. Callbacks (<see cref="OnHttpRequest"/>
    /// and <see cref="OnWebSocketConnected"/>) run on those background threads —
    /// callers must marshal Unity-API work onto the main thread themselves.
    /// </summary>
    public sealed class MiniHttpServer : IDisposable
    {
        public Func<MiniHttpRequest, MiniHttpResponse, Task> OnHttpRequest;
        public Func<MiniHttpRequest, MiniWebSocket, Task> OnWebSocketConnected;

        TcpListener m_Listener;
        CancellationTokenSource m_Cts;
        public bool IsRunning => m_Listener != null;

        public void Start(int port, bool bindAllInterfaces)
        {
            Stop();
            m_Cts = new CancellationTokenSource();

            var endpoint = bindAllInterfaces
                ? new IPEndPoint(IPAddress.Any, port)
                : new IPEndPoint(IPAddress.Loopback, port);

            m_Listener = new TcpListener(endpoint);
            m_Listener.Start();

            _ = AcceptLoop(m_Cts.Token);
        }

        public void Stop()
        {
            try { m_Cts?.Cancel(); } catch { }
            try { m_Listener?.Stop(); } catch { }
            m_Listener = null;
            m_Cts = null;
        }

        public void Dispose() => Stop();

        async Task AcceptLoop(CancellationToken ct)
        {
            var listener = m_Listener;
            while (!ct.IsCancellationRequested && listener != null)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(); }
                catch { return; }
                _ = HandleClient(client, ct);
            }
        }

        async Task HandleClient(TcpClient client, CancellationToken ct)
        {
            try
            {
                client.NoDelay = true;
                using var stream = client.GetStream();

                MiniHttpRequest req;
                try { req = await ParseRequest(stream, client.Client.RemoteEndPoint as IPEndPoint, ct); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MiniHttpServer] request parse failed: {e.Message}");
                    return;
                }
                if (req == null) return;

                // WebSocket upgrade
                if (req.Headers.TryGetValue("upgrade", out var up)
                    && up.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await HandleUpgrade(req, stream, ct);
                    return; // connection now owned by the WS handler
                }

                // Plain HTTP
                var resp = new MiniHttpResponse();
                if (OnHttpRequest != null)
                {
                    try { await OnHttpRequest(req, resp); }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        resp.StatusCode = 500;
                        resp.SetText($"server error: {e.Message}");
                    }
                }
                await WriteResponse(resp, stream, ct);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MiniHttpServer] connection error: {e.Message}");
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        // ---- HTTP parsing ----

        static async Task<MiniHttpRequest> ParseRequest(NetworkStream stream, IPEndPoint remote, CancellationToken ct)
        {
            // Read header bytes until we hit \r\n\r\n. Then optionally read body.
            var headerBytes = new MemoryStream();
            var buf = new byte[1];
            int matched = 0; // tracks position in "\r\n\r\n"
            const int maxHeaders = 32 * 1024;

            while (matched < 4)
            {
                int n = await stream.ReadAsync(buf, 0, 1, ct);
                if (n == 0) return null;
                headerBytes.WriteByte(buf[0]);
                if (headerBytes.Length > maxHeaders) throw new InvalidOperationException("headers too large");
                byte b = buf[0];
                if ((matched == 0 && b == 0x0D)
                 || (matched == 1 && b == 0x0A)
                 || (matched == 2 && b == 0x0D)
                 || (matched == 3 && b == 0x0A))
                    matched++;
                else
                    matched = (b == 0x0D) ? 1 : 0;
            }

            string headerBlock = Encoding.ASCII.GetString(headerBytes.GetBuffer(), 0, (int)headerBytes.Length);
            string[] lines = headerBlock.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length < 1) return null;

            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 3) return null;

            var req = new MiniHttpRequest
            {
                Method = requestLine[0],
                Path = requestLine[1],
                Version = requestLine[2],
                RemoteEndPoint = remote,
            };

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                string key = lines[i].Substring(0, colon).Trim().ToLowerInvariant();
                string value = lines[i].Substring(colon + 1).Trim();
                req.Headers[key] = value;
            }

            // Body (Content-Length only — no chunked support)
            if (req.Headers.TryGetValue("content-length", out var clStr)
                && int.TryParse(clStr, out int cl) && cl > 0)
            {
                var body = new byte[cl];
                int read = 0;
                while (read < cl)
                {
                    int got = await stream.ReadAsync(body, read, cl - read, ct);
                    if (got == 0) break;
                    read += got;
                }
                req.Body = Encoding.UTF8.GetString(body, 0, read);
            }

            return req;
        }

        static async Task WriteResponse(MiniHttpResponse resp, NetworkStream stream, CancellationToken ct)
        {
            byte[] body = resp.Body ?? Array.Empty<byte>();

            var sb = new StringBuilder(256);
            sb.Append("HTTP/1.1 ").Append(resp.StatusCode).Append(' ').Append(StatusText(resp.StatusCode)).Append("\r\n");

            if (!resp.Headers.ContainsKey("content-length") && resp.BodyStream == null)
                sb.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            if (!resp.Headers.ContainsKey("content-type"))
                sb.Append("Content-Type: text/plain; charset=utf-8\r\n");
            // No keep-alive — simpler shutdown semantics.
            sb.Append("Connection: close\r\n");

            foreach (var kv in resp.Headers)
                sb.Append(CapitalizeHeaderName(kv.Key)).Append(": ").Append(kv.Value).Append("\r\n");

            sb.Append("\r\n");
            byte[] header = Encoding.ASCII.GetBytes(sb.ToString());
            await stream.WriteAsync(header, 0, header.Length, ct);

            if (resp.BodyStream != null)
            {
                await resp.BodyStream.CopyToAsync(stream);
            }
            else if (body.Length > 0)
            {
                await stream.WriteAsync(body, 0, body.Length, ct);
            }
        }

        static string StatusText(int code) => code switch
        {
            101 => "Switching Protocols",
            200 => "OK",
            204 => "No Content",
            302 => "Found",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            500 => "Internal Server Error",
            501 => "Not Implemented",
            _   => "OK",
        };

        static string CapitalizeHeaderName(string lower)
        {
            // Cosmetic only; headers are case-insensitive.
            var parts = lower.Split('-');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join("-", parts);
        }

        // ---- WebSocket upgrade ----

        async Task HandleUpgrade(MiniHttpRequest req, NetworkStream stream, CancellationToken ct)
        {
            if (!req.Headers.TryGetValue("sec-websocket-key", out var key) || string.IsNullOrEmpty(key))
            {
                await WriteSimple(stream, 400, "missing Sec-WebSocket-Key", ct);
                return;
            }

            string accept = ComputeAcceptKey(key);
            string handshake =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";

            byte[] bytes = Encoding.ASCII.GetBytes(handshake);
            await stream.WriteAsync(bytes, 0, bytes.Length, ct);

            var ws = new MiniWebSocket(stream);
            try
            {
                if (OnWebSocketConnected != null)
                    await OnWebSocketConnected(req, ws);
            }
            finally
            {
                ws.Dispose();
            }
        }

        static string ComputeAcceptKey(string key)
        {
            const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + magic));
            return Convert.ToBase64String(hash);
        }

        static async Task WriteSimple(NetworkStream stream, int code, string text, CancellationToken ct)
        {
            byte[] body = Encoding.UTF8.GetBytes(text);
            string head = $"HTTP/1.1 {code} {StatusText(code)}\r\nContent-Length: {body.Length}\r\nContent-Type: text/plain; charset=utf-8\r\nConnection: close\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(head);
            await stream.WriteAsync(hb, 0, hb.Length, ct);
            if (body.Length > 0) await stream.WriteAsync(body, 0, body.Length, ct);
        }
    }

    // ---- Request / Response ----

    public sealed class MiniHttpRequest
    {
        public string Method;
        public string Path;
        public string Version;
        public IPEndPoint RemoteEndPoint;
        public Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
        public string Body = "";
    }

    public sealed class MiniHttpResponse
    {
        public int StatusCode = 200;
        public Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
        public byte[] Body;
        public Stream BodyStream;

        public void SetText(string text, string contentType = "text/plain; charset=utf-8")
        {
            Body = Encoding.UTF8.GetBytes(text ?? "");
            Headers["content-type"] = contentType;
        }

        public void SetJson(string json) => SetText(json, "application/json; charset=utf-8");

        public void SetFile(Stream s, long length, string contentType)
        {
            BodyStream = s;
            Headers["content-type"] = contentType;
            Headers["content-length"] = length.ToString();
        }
    }

    // ---- WebSocket connection ----

    public sealed class MiniWebSocket : IDisposable
    {
        readonly NetworkStream m_Stream;
        readonly SemaphoreSlim m_SendLock = new(1, 1);
        volatile bool m_Closed;

        public bool IsOpen => !m_Closed && m_Stream != null;

        public MiniWebSocket(NetworkStream stream) { m_Stream = stream; }

        public async Task SendTextAsync(string text, CancellationToken ct = default)
        {
            if (m_Closed) return;
            byte[] payload = Encoding.UTF8.GetBytes(text ?? "");
            await m_SendLock.WaitAsync(ct);
            try { await SendFrame(0x1, payload, ct); }
            finally { m_SendLock.Release(); }
        }

        public async Task CloseAsync(CancellationToken ct = default)
        {
            if (m_Closed) return;
            await m_SendLock.WaitAsync(ct);
            try
            {
                try { await SendFrame(0x8, Array.Empty<byte>(), ct); } catch { }
                m_Closed = true;
            }
            finally { m_SendLock.Release(); }
        }

        async Task SendFrame(byte opcode, byte[] payload, CancellationToken ct)
        {
            if (m_Stream == null) return;
            byte b0 = (byte)(0x80 | opcode); // FIN=1
            int len = payload.Length;

            byte[] header;
            if (len < 126)
            {
                header = new byte[2];
                header[0] = b0;
                header[1] = (byte)len; // mask=0
            }
            else if (len < 65536)
            {
                header = new byte[4];
                header[0] = b0;
                header[1] = 126;
                header[2] = (byte)((len >> 8) & 0xFF);
                header[3] = (byte)(len & 0xFF);
            }
            else
            {
                header = new byte[10];
                header[0] = b0;
                header[1] = 127;
                long llen = len;
                for (int i = 0; i < 8; i++)
                    header[2 + i] = (byte)((llen >> ((7 - i) * 8)) & 0xFF);
            }

            await m_Stream.WriteAsync(header, 0, header.Length, ct);
            if (payload.Length > 0)
                await m_Stream.WriteAsync(payload, 0, payload.Length, ct);
        }

        /// <summary>
        /// Reads one application-level message. Handles control frames (ping → pong,
        /// close → return null) transparently. Returns the text payload, or null
        /// if the peer closed the connection.
        /// </summary>
        public async Task<string> ReceiveTextAsync(CancellationToken ct = default)
        {
            while (!m_Closed)
            {
                var head = await ReadExactly(2, ct);
                if (head == null) { m_Closed = true; return null; }

                bool fin = (head[0] & 0x80) != 0;
                byte opcode = (byte)(head[0] & 0x0F);
                bool masked = (head[1] & 0x80) != 0;
                long payloadLen = head[1] & 0x7F;

                if (payloadLen == 126)
                {
                    var ext = await ReadExactly(2, ct);
                    if (ext == null) { m_Closed = true; return null; }
                    payloadLen = (ext[0] << 8) | ext[1];
                }
                else if (payloadLen == 127)
                {
                    var ext = await ReadExactly(8, ct);
                    if (ext == null) { m_Closed = true; return null; }
                    payloadLen = 0;
                    for (int i = 0; i < 8; i++) payloadLen = (payloadLen << 8) | ext[i];
                }

                byte[] mask = null;
                if (masked)
                {
                    mask = await ReadExactly(4, ct);
                    if (mask == null) { m_Closed = true; return null; }
                }

                byte[] payload = payloadLen == 0 ? Array.Empty<byte>() : await ReadExactly((int)payloadLen, ct);
                if (payload == null) { m_Closed = true; return null; }

                if (masked)
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] ^= mask[i % 4];

                switch (opcode)
                {
                    case 0x1: // text
                        return Encoding.UTF8.GetString(payload);
                    case 0x2: // binary — not used; ignore and continue
                        continue;
                    case 0x8: // close
                        await m_SendLock.WaitAsync(ct);
                        try { try { await SendFrame(0x8, Array.Empty<byte>(), ct); } catch { } }
                        finally { m_SendLock.Release(); }
                        m_Closed = true;
                        return null;
                    case 0x9: // ping → pong
                        await m_SendLock.WaitAsync(ct);
                        try { try { await SendFrame(0xA, payload, ct); } catch { } }
                        finally { m_SendLock.Release(); }
                        continue;
                    case 0xA: // pong — ignore
                        continue;
                    default:
                        continue;
                }
            }
            return null;
        }

        async Task<byte[]> ReadExactly(int n, CancellationToken ct)
        {
            if (n <= 0) return Array.Empty<byte>();
            var buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int got;
                try { got = await m_Stream.ReadAsync(buf, read, n - read, ct); }
                catch { return null; }
                if (got == 0) return null;
                read += got;
            }
            return buf;
        }

        public void Dispose()
        {
            m_Closed = true;
            try { m_Stream?.Dispose(); } catch { }
        }
    }
}
