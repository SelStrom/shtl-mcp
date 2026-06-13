using NUnit.Framework;
using Newtonsoft.Json.Linq;
using ShtlMcp.Logging;
using ShtlMcp.Tools;

namespace ShtlMcp.Editor.Tests
{
    public class GetLogsToolTests
    {
        [Test] public void Invoke_ReturnsRecentLogs_RespectingCount()
        {
            var buf = new LogBuffer(10);
            buf.Add("first", "", LogLevel.Info);
            buf.Add("boom", "stack", LogLevel.Error);
            var tool = new GetLogsTool(buf);

            var o = tool.Invoke(new JObject { ["count"] = 1 });
            var logs = (JArray)o["logs"];
            Assert.AreEqual(1, logs.Count);
            Assert.AreEqual("boom", (string)logs[0]["message"]);
            Assert.AreEqual("error", (string)logs[0]["level"]);
        }

        [Test] public void Invoke_FiltersByMinLevel()
        {
            var buf = new LogBuffer(10);
            buf.Add("i", "", LogLevel.Info);
            buf.Add("e", "", LogLevel.Error);
            var o = new GetLogsTool(buf).Invoke(new JObject { ["minLevel"] = "error", ["count"] = 10 });
            Assert.AreEqual(1, ((JArray)o["logs"]).Count);
            Assert.AreEqual("e", (string)((JArray)o["logs"])[0]["message"]);
        }
    }
}
