using Newtonsoft.Json.Linq;
using Shtl.Mcp.Logging;

namespace Shtl.Mcp.Tools
{
    /// Очистить буфер консольных логов (тот, что отдаёт get_logs). Не требует главного потока
    /// (LogBuffer потокобезопасен).
    public sealed class ClearLogsTool : ITool
    {
        readonly LogBuffer _logs;

        public ClearLogsTool(LogBuffer logs)
        {
            _logs = logs;
        }

        public string Name => "clear_logs";
        public string Description => "Clear the captured Unity console log buffer (the one get_logs reads).";
        public bool NeedsMainThread => false;
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args) => new JObject { ["cleared"] = _logs.Clear() };
    }
}
