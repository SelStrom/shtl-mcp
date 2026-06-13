using System;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using Shtl.Mcp.Common;
using Shtl.Mcp.Dispatch;
using Shtl.Mcp.Logging;
using Shtl.Mcp.Registry;
using Shtl.Mcp.Server;
using Shtl.Mcp.Tools;
using Shtl.Mcp.Transport;

namespace Shtl.Mcp.Lifecycle
{
    public sealed class ShtlMcpServer
    {
        static ShtlMcpServer _instance;
        public static ShtlMcpServer Instance => _instance ?? (_instance = new ShtlMcpServer());

        const string PortKey = "Shtl.Mcp.Port";
        const string StartedKey = "Shtl.Mcp.StartedTicks";
        static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
        static readonly string RegistryPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-mcp", "registry.json");

        readonly MainThreadDispatcher _dispatcher = new MainThreadDispatcher();
        readonly LogBuffer _logs = new LogBuffer(500);
        readonly ToolRegistry _tools = new ToolRegistry();
        readonly RegistryStore _registry = new RegistryStore(RegistryPath);

        HttpServer _http;
        string _serverName;
        DateTime _lastRequestUtc = DateTime.MinValue;

        public int Port { get; private set; }
        public string ServerName => _serverName;
        public bool IsListening => _http != null && _http.IsListening;

        string ProjectPath => System.IO.Directory.GetParent(Application.dataPath).FullName;
        double Uptime => (DateTime.UtcNow - new DateTime(long.Parse(
            SessionState.GetString(StartedKey, DateTime.UtcNow.Ticks.ToString())), DateTimeKind.Utc)).TotalSeconds;

        public void EnsureStarted()
        {
            if (IsListening)
            {
                return;
            }

            if (SessionState.GetString(StartedKey, "") == "")
            {
                SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            }

            Port = ResolvePort();
            _serverName = Shtl.Mcp.Common.ServerName.Resolve(Application.productName, ProjectPath,
                name => _registry.LivePathForName(name, Ttl));

            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;

            EditorApplication.update -= _dispatcher.Drain;
            EditorApplication.update += _dispatcher.Drain;

            var ctx = new EditorContext(() => Port, () => _serverName, () => Uptime, ClientCount);
            _tools.Register(new StatusTool(ctx));
            _tools.Register(new GetLogsTool(_logs));

            var invoker = new DispatchingToolInvoker(_tools, _dispatcher);
            var info = new ServerInfo
            {
                Name = _serverName,
                Version = "0.1.0",
                Instructions = "Unity MCP. Если станет недоступен — читай ~/.unity-mcp/registry.json."
            };
            var router = new McpRouter(invoker, info);

            _http = new HttpServer(Port, router.Handle, () => _lastRequestUtc = DateTime.UtcNow);
            _http.Start();
            if (!_http.IsListening)
            {
                // bind не удался (порт ещё занят) — сбрасываем, watchdog повторит на следующем тике
                _http = null;
                return;
            }
            Heartbeat();
        }

        public void StopListenerForReload()
        {
            _http?.Stop();
            _http = null;
        }

        public void RestartNow()
        {
            StopListenerForReload();
            EnsureStarted();
        }

        public void WatchdogTick()
        {
            if (!ShtlMcpConfig.Enabled)
            {
                StopListenerForReload();
                return;
            }

            if (!IsListening)
            {
                EnsureStarted();
            }

            Heartbeat();
        }

        int ClientCount() => (DateTime.UtcNow - _lastRequestUtc) < TimeSpan.FromSeconds(30) ? 1 : 0;

        int ResolvePort()
        {
            int saved = SessionState.GetInt(PortKey, 0);
            if (saved != 0)
            {
                return saved;
            }

            int port = PortAllocator.Allocate(ProjectPath, IsPortFree);
            SessionState.SetInt(PortKey, port);
            return port;
        }

        static bool IsPortFree(int port)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                l.Start(); l.Stop(); return true;
            }
            catch (SocketException) { return false; }
        }

        void OnLog(string message, string stack, LogType type)
            => _logs.Add(message, stack,
                type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? LogLevel.Error :
                type == LogType.Warning ? LogLevel.Warning : LogLevel.Info);

        void Heartbeat()
        {
            try
            {
                _registry.Upsert(new InstanceEntry
                {
                    ProjectName = Application.productName,
                    ProjectPath = ProjectPath,
                    UnityVersion = Application.unityVersion,
                    ServerName = _serverName,
                    Port = Port,
                    Pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    Mode = EditorApplication.isPlaying ? "play" : "edit",
                    Compiling = EditorApplication.isCompiling,
                    StartedAt = new DateTime(long.Parse(SessionState.GetString(StartedKey,
                        DateTime.UtcNow.Ticks.ToString())), DateTimeKind.Utc),
                    LastHeartbeat = DateTime.UtcNow
                });
            }
            catch { }
        }
    }
}
