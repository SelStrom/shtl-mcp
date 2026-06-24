using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Tools
{
    public sealed class StatusTool : ITool
    {
        readonly IEditorContext _ctx;
        public StatusTool(IEditorContext ctx) => _ctx = ctx;

        public string Name => "status";
        public string Description => "Identity and health of this Unity instance (project, port, mode, compiling).";
        public bool NeedsMainThread => true; // читает EditorApplication.*
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args) => new JObject
        {
            ["projectName"] = _ctx.ProjectName,
            ["projectPath"] = _ctx.ProjectPath,
            ["unityVersion"] = _ctx.UnityVersion,
            ["serverName"] = _ctx.ServerName,
            ["port"] = _ctx.Port,
            ["pid"] = _ctx.Pid,
            ["mode"] = _ctx.IsPlaying ? "play" : "edit",
            ["isCompiling"] = _ctx.IsCompiling,
            ["uptimeSeconds"] = (int)_ctx.UptimeSeconds,
            ["listenerUptimeSeconds"] = (int)_ctx.ListenerUptimeSeconds,
            ["reloadCount"] = _ctx.ReloadCount,
            ["clients"] = _ctx.ClientCount,
            ["health"] = "ok",
            // recovery-playbook (AC2.7): если сервер перестал отвечать, но Unity жив — форс-рестарт через
            // control-flag (watchdog исполнит на ближайшем тике). Полная F7-discoverability — M3.
            ["recovery"] = "If this server stops responding while Unity is running, write 'restart' to " +
                "~/.unity-mcp/" + _ctx.ServerName + ".cmd; the editor watchdog recreates the listener. " +
                "See ~/.unity-mcp/registry.json for instance details."
        };
    }
}
