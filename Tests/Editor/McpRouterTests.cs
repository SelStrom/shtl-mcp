using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Transport;

namespace Shtl.Mcp.Editor.Tests
{
    class FakeInvoker : IToolInvoker
    {
        public JArray Tools = new JArray {
            new JObject { ["name"] = "status", ["description"] = "x", ["inputSchema"] = new JObject { ["type"] = "object" } }
        };
        public JArray ListTools() => Tools;
        public JObject Invoke(string name, JObject args)
        {
            if (name == "status")
            {
                return new JObject { ["projectName"] = "PW" };
            }
            if (name == "img")
            {
                return new JObject
                {
                    ["_content"] = new JArray
                    {
                        new JObject { ["type"] = "image", ["data"] = "AAA", ["mimeType"] = "image/png" }
                    }
                };
            }
            throw new System.Exception("unknown tool: " + name);
        }
    }

    public class McpRouterTests
    {
        McpRouter NewRouter() => new McpRouter(new FakeInvoker(),
            new ServerInfo { Name = "unity-pw", Version = "0.1.0", Instructions = "hi" });

        [Test] public void Initialize_ReturnsServerInfoAndCapabilities()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":1,""method"":""initialize"",""params"":{}}"));
            Assert.AreEqual("unity-pw", (string)o["result"]["serverInfo"]["name"]);
            Assert.IsNotNull(o["result"]["capabilities"]["tools"]);
            Assert.AreEqual("hi", (string)o["result"]["instructions"]);
        }

        [Test] public void InitializedNotification_ReturnsEmpty()
        {
            string r = NewRouter().Handle(@"{""jsonrpc"":""2.0"",""method"":""notifications/initialized""}");
            Assert.IsTrue(string.IsNullOrEmpty(r));
        }

        [Test] public void ToolsList_ReturnsRegisteredTools()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":2,""method"":""tools/list"",""params"":{}}"));
            Assert.AreEqual("status", (string)o["result"]["tools"][0]["name"]);
        }

        [Test] public void ToolsCall_WrapsResultAsTextContent()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":3,""method"":""tools/call"",""params"":{""name"":""status"",""arguments"":{}}}"));
            Assert.IsFalse((bool)o["result"]["isError"]);
            Assert.AreEqual("text", (string)o["result"]["content"][0]["type"]);
            StringAssert.Contains("PW", (string)o["result"]["content"][0]["text"]);
        }

        [Test] public void ToolsCall_PassesThrough_ContentConvention()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":6,""method"":""tools/call"",""params"":{""name"":""img"",""arguments"":{}}}"));
            Assert.IsFalse((bool)o["result"]["isError"]);
            Assert.AreEqual("image", (string)o["result"]["content"][0]["type"]);
            Assert.AreEqual("image/png", (string)o["result"]["content"][0]["mimeType"]);
        }

        [Test] public void UnknownMethod_Returns_MethodNotFound()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":4,""method"":""bogus""}"));
            Assert.AreEqual(-32601, (int)o["error"]["code"]);
        }

        [Test] public void ParseError_Returns_ParseError()
        {
            var o = JObject.Parse(NewRouter().Handle("{ this is not json"));
            Assert.AreEqual(-32700, (int)o["error"]["code"]);
        }

        [Test] public void ToolThrows_Returns_IsErrorContent()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":5,""method"":""tools/call"",""params"":{""name"":""nope"",""arguments"":{}}}"));
            Assert.IsTrue((bool)o["result"]["isError"]);
        }
    }
}
