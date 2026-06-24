using System;
using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Tools
{
    /// Текущая конфигурация сервера. Read-only: footgun-флаги меняет человек в Editor (UI → M3), не MCP-тул.
    /// Снимок инжектируется (Func) — Tools не зависит от Lifecycle (DAG сборок цел).
    public sealed class GetConfigTool : ITool
    {
        readonly Func<JObject> _snapshot;

        public GetConfigTool(Func<JObject> snapshot)
        {
            _snapshot = snapshot;
        }

        public string Name => "get_config";
        public string Description =>
            "Get the current server configuration (enabled, port range, heartbeat, allowRunCsharp, " +
            "effective port/serverName). Read-only — change via the Unity Editor.";
        public bool NeedsMainThread => true;
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args) => _snapshot();
    }
}
