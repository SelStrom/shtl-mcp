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
            ["clients"] = _ctx.ClientCount,
            ["health"] = "ok"
        };
    }
}
