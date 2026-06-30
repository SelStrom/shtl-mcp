using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Transport
{
    /// Чистый string→string. MCP минимум: initialize, notifications/initialized, tools/list, tools/call.
    public sealed class McpRouter
    {
        const string ProtocolVersion = "2024-11-05";
        readonly IToolInvoker _tools;
        readonly ServerInfo _info;
        readonly string _projectName;   // INV-3: идентичность инстанса в КАЖДОМ ответе тула
        readonly string _recoveryHint;  // F7/AC7.3: терсный указатель «где смотреть при недоступности»
        readonly Action<string, bool, double> _recordCall; // F5/AC5.5: хвост вызовов (метод, ok, мс); опц.

        public McpRouter(IToolInvoker tools, ServerInfo info, string projectName, string recoveryHint,
            Action<string, bool, double> recordCall = null)
        {
            _tools = tools;
            _info = info;
            _projectName = projectName;
            _recoveryHint = recoveryHint;
            _recordCall = recordCall;
        }

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
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var result = _tools.Invoke(name, args);
                        bool ok = result["error"] == null; // тул вернул {error} → логическая ошибка (✗)
                        JArray content;
                        if (result["_content"] is JArray c)
                        {
                            // image/готовый content — идентичность+hint отдельным text-элементом (в JSON их не вставить)
                            c.Add(new JObject { ["type"] = "text", ["text"] = "projectName: " + _projectName + "\nrecoveryHint: " + _recoveryHint });
                            content = c;
                        }
                        else
                        {
                            result["projectName"] = _projectName;   // INV-3: идентичность в самом JSON-результате
                            result["recoveryHint"] = _recoveryHint; // F7/AC7.3
                            content = new JArray { new JObject { ["type"] = "text", ["text"] = result.ToString() } };
                        }
                        _recordCall?.Invoke(name, ok, sw.Elapsed.TotalMilliseconds);
                        return JsonRpc.Result(id, new JObject
                        {
                            ["content"] = content,
                            ["isError"] = false
                        });
                    }
                    catch (System.Exception e)
                    {
                        _recordCall?.Invoke(name, false, sw.Elapsed.TotalMilliseconds);
                        return JsonRpc.Result(id, new JObject
                        {
                            ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = "[" + _projectName + "] Error: " + e.Message } },
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
