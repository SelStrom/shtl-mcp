using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Transport;

namespace Shtl.Mcp.Editor.Tests
{
    public class JsonRpcTests
    {
        [Test] public void Error_BuildsStandardEnvelope()
        {
            string s = JsonRpc.Error(7, -32601, "Method not found");
            var o = JObject.Parse(s);
            Assert.AreEqual("2.0", (string)o["jsonrpc"]);
            Assert.AreEqual(7, (int)o["id"]);
            Assert.AreEqual(-32601, (int)o["error"]["code"]);
        }

        [Test] public void Result_BuildsEnvelope()
        {
            string s = JsonRpc.Result(1, new JObject { ["ok"] = true });
            var o = JObject.Parse(s);
            Assert.AreEqual(1, (int)o["id"]);
            Assert.IsTrue((bool)o["result"]["ok"]);
        }
    }
}
