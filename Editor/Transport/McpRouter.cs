using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Transport
{
    /// Чистый string→string. MCP минимум: initialize, notifications/initialized, tools/list, tools/call.
    public sealed class McpRouter
    {
        const string ProtocolVersion = "2024-11-05";
        readonly IToolInvoker _tools;
        readonly ServerInfo _info;

        public McpRouter(IToolInvoker tools, ServerInfo info) { _tools = tools; _info = info; }

        public string Handle(string requestJson)
        {
            JObject req;
            try { req = JObject.Parse(requestJson); }
            catch { return JsonRpc.Error(null, -32700, "Parse error"); }

            string method = (string)req["method"];
            JToken id = req["id"];

            // нет id → нотификация: ответа не шлём
            if (id == null && method != null && method.StartsWith("notifications/"))
            {
                return "";
            }

            switch (method)
            {
                case "initialize":
                    return JsonRpc.Result(id, new JObject
                    {
                        ["protocolVersion"] = ProtocolVersion,
                        ["capabilities"] = new JObject { ["tools"] = new JObject() },
                        ["serverInfo"] = new JObject { ["name"] = _info.Name, ["version"] = _info.Version },
                        ["instructions"] = _info.Instructions ?? ""
                    });

                case "tools/list":
                    return JsonRpc.Result(id, new JObject { ["tools"] = _tools.ListTools() });

                case "tools/call":
                {
                    var p = (JObject)req["params"] ?? new JObject();
                    string name = (string)p["name"];
                    var args = (JObject)p["arguments"] ?? new JObject();
                    try
                    {
                        var result = _tools.Invoke(name, args);
                        return JsonRpc.Result(id, new JObject
                        {
                            ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = result.ToString() } },
                            ["isError"] = false
                        });
                    }
                    catch (System.Exception e)
                    {
                        return JsonRpc.Result(id, new JObject
                        {
                            ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = "Error: " + e.Message } },
                            ["isError"] = true
                        });
                    }
                }

                default:
                    return JsonRpc.Error(id, -32601, "Method not found: " + method);
            }
        }
    }
}
