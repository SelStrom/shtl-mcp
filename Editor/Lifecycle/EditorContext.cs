using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Lifecycle
{
    public sealed class EditorContext : IEditorContext
    {
        readonly Func<int> _port;
        readonly Func<string> _serverName;
        readonly Func<double> _uptime;
        readonly Func<int> _clients;
        readonly Func<double> _listenerUptime;
        readonly Func<int> _reloadCount;

        public EditorContext(Func<int> port, Func<string> serverName, Func<double> uptime, Func<int> clients,
            Func<double> listenerUptime, Func<int> reloadCount)
        {
            _port = port;
            _serverName = serverName;
            _uptime = uptime;
            _clients = clients;
            _listenerUptime = listenerUptime;
            _reloadCount = reloadCount;
        }

        public string ProjectName => Application.productName;
        public string ProjectPath => System.IO.Directory.GetParent(Application.dataPath).FullName;
        public string UnityVersion => Application.unityVersion;
        public string ServerName => _serverName();
        public int Port => _port();
        public int Pid => Process.GetCurrentProcess().Id;
        public bool IsPlaying => EditorApplication.isPlaying;
        public bool IsCompiling => EditorApplication.isCompiling;
        public double UptimeSeconds => _uptime();
        public double ListenerUptimeSeconds => _listenerUptime();
        public int ReloadCount => _reloadCount();
        public int ClientCount => _clients();
    }
}
