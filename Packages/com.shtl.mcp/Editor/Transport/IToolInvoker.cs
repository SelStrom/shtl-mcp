using Newtonsoft.Json.Linq;

namespace ShtlMcp.Transport
{
    public interface IToolInvoker
    {
        JArray ListTools();                        // [{name, description, inputSchema}]
        JObject Invoke(string name, JObject args); // результат инструмента (или бросает)
    }
}
