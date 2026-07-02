using Newtonsoft.Json.Linq;
using Shtl.Mcp.Tools;

namespace Host.Editor.McpTools
{
    /// Пример кастомного инструмента host-проекта (F3/AC3.6). Живёт в Editor-сборке хоста, которая референсит
    /// `Shtl.Mcp.Tools` (+ `Newtonsoft.Json`). Помечен `[McpTool]` + реализует `ITool` → сервер обнаруживает его
    /// рефлексией и регистрирует БЕЗ правок shtl-mcp и без ручной регистрации. После рекомпиляции появляется в
    /// `tools/list` как `greet` (клиенту может понадобиться reconnect).
    [McpTool]
    public sealed class GreetTool : ITool
    {
        public string Name => "greet";
        public string Description => "Example custom tool: return a greeting for the given name.";

        // Чистый CLR → фоновый HTTP-поток. Поставь true, если внутри Invoke трогаешь UnityEditor/UnityEngine API.
        public bool NeedsMainThread => false;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["name"] = new JObject { ["type"] = "string", ["description"] = "Who to greet." }
            },
            ["required"] = new JArray { "name" }
        };

        public JObject Invoke(JObject args)
        {
            var name = (string)args["name"];
            if (string.IsNullOrEmpty(name))
            {
                return new JObject { ["error"] = "name is required" }; // структурированная ошибка, не исключение
            }
            return new JObject { ["greeting"] = "Hello, " + name + "!" };
        }
    }
}
