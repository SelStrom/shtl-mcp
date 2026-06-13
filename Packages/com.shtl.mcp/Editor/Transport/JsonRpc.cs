using Newtonsoft.Json.Linq;

namespace ShtlMcp.Transport
{
    public sealed class ServerInfo
    {
        public string Name;
        public string Version;
        public string Instructions;
    }

    public static class JsonRpc
    {
        public static string Result(JToken id, JToken result)
            => new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToString();

        public static string Error(JToken id, int code, string message)
            => new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message }
            }.ToString();
    }
}
