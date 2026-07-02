using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Tools
{
    public sealed class ToolRegistry
    {
        readonly Dictionary<string, ITool> _tools = new Dictionary<string, ITool>();
        public void Register(ITool tool) => _tools[tool.Name] = tool;
        public ITool Get(string name) => _tools.TryGetValue(name, out var t) ? t : null;

        /// Уже зарегистрирован тул с таким именем? Дискавери кастомных использует это, чтобы НЕ перетирать
        /// встроенные (они регистрируются первыми) и не давать кастом-vs-кастом-override (F3/AC3.7).
        public bool Contains(string name) => _tools.ContainsKey(name);

        public JArray List()
        {
            var arr = new JArray();
            foreach (var t in _tools.Values)
            {
                arr.Add(new JObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["inputSchema"] = t.InputSchema
                });
            }
            return arr;
        }
    }
}
