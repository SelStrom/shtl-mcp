using Newtonsoft.Json.Linq;

namespace ShtlMcp.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        JObject InputSchema { get; }
        bool NeedsMainThread { get; }
        JObject Invoke(JObject args);
    }
}
