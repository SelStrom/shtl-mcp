using Newtonsoft.Json.Linq;
using ShtlMcp.Dispatch;
using ShtlMcp.Tools;
using ShtlMcp.Transport;

namespace ShtlMcp.Server
{
    /// Маршалит вызовы инструментов в главный поток, когда NeedsMainThread.
    public sealed class DispatchingToolInvoker : IToolInvoker
    {
        readonly ToolRegistry _registry;
        readonly MainThreadDispatcher _dispatcher;
        const int TimeoutMs = 5000;

        public DispatchingToolInvoker(ToolRegistry registry, MainThreadDispatcher dispatcher)
        { _registry = registry; _dispatcher = dispatcher; }

        public JArray ListTools() => _registry.List();

        public JObject Invoke(string name, JObject args)
        {
            var tool = _registry.Get(name);
            if (tool == null)
            {
                throw new System.Exception("Unknown tool: " + name);
            }
            return tool.NeedsMainThread
                ? _dispatcher.RunOnMain(() => tool.Invoke(args), TimeoutMs)
                : tool.Invoke(args);
        }
    }
}
