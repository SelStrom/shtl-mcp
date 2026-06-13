using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace ShtlMcp.Server
{
    /// Фоновый HttpListener на 127.0.0.1:port. Любой путь трактуется как MCP-эндпойнт.
    public sealed class HttpServer
    {
        readonly Func<string, string> _handle;     // McpRouter.Handle
        readonly Action _onRequest;                 // отметка активности (clients/uptime)
        HttpListener _listener;
        Thread _thread;
        volatile bool _running;

        public int Port { get; }
        public bool IsListening => _listener != null && _listener.IsListening;

        public HttpServer(int port, Func<string, string> handle, Action onRequest)
        { Port = port; _handle = handle; _onRequest = onRequest; }

        public void Start()
        {
            if (_running)
            {
                return;
            }
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ShtlMcpHttp" };
            _thread.Start();
        }

        void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch
                {
                    break; // listener остановлен
                }
                try
                {
                    _onRequest?.Invoke();
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        ctx.Response.StatusCode = 405; // нет server-initiated SSE в M1
                    }
                    else
                    {
                        string body;
                        using (var r = new StreamReader(ctx.Request.InputStream,
                                   ctx.Request.ContentEncoding ?? Encoding.UTF8))
                        {
                            body = r.ReadToEnd();
                        }

                        string resp = _handle(body) ?? "";
                        var bytes = Encoding.UTF8.GetBytes(resp);
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.StatusCode = resp.Length == 0 ? 202 : 200;
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch
                {
                    // одиночный сбойный запрос не валит цикл
                }
                finally
                {
                    try
                    {
                        ctx.Response.OutputStream.Close();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // ignored
            }
            _listener = null;
        }
    }
}
