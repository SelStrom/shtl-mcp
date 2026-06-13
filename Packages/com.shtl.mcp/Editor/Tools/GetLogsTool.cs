using Newtonsoft.Json.Linq;
using ShtlMcp.Logging;

namespace ShtlMcp.Tools
{
    public sealed class GetLogsTool : ITool
    {
        readonly LogBuffer _buffer;
        public GetLogsTool(LogBuffer buffer) => _buffer = buffer;

        public string Name => "get_logs";
        public string Description => "Recent Unity console logs (filter by minLevel: info|warning|error).";
        public bool NeedsMainThread => false; // LogBuffer потокобезопасен
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["minLevel"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "info", "warning", "error" } },
                ["count"] = new JObject { ["type"] = "integer", ["default"] = 50 }
            }
        };

        public JObject Invoke(JObject args)
        {
            int count = (int?)args["count"] ?? 50;
            LogLevel? min = ParseLevel((string)args["minLevel"]);
            var arr = new JArray();
            foreach (var it in _buffer.Get(min, count))
            {
                arr.Add(new JObject
                {
                    ["message"] = it.Message,
                    ["level"] = it.Level.ToString().ToLowerInvariant(),
                    ["stack"] = it.Stack
                });
            }
            return new JObject { ["logs"] = arr };
        }

        static LogLevel? ParseLevel(string s)
        {
            switch (s)
            {
                case "warning": return LogLevel.Warning;
                case "error": return LogLevel.Error;
                case "info": return LogLevel.Info;
                default: return null;
            }
        }
    }
}
