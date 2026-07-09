using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shtl.Mcp.Lifecycle;
using Shtl.Mcp.Logging;

namespace Shtl.Mcp.Editor.Tests
{
    public class LogCaptureTests
    {
        [Test] public void Serialize_Deserialize_RoundTrips()
        {
            var items = new List<LogItem>
            {
                new LogItem("info msg", "", LogLevel.Info),
                new LogItem("warn msg", "at Foo:12", LogLevel.Warning),
                new LogItem("err msg", "at Bar:3\nat Baz:9", LogLevel.Error),
            };

            var restored = LogCapture.Deserialize(LogCapture.Serialize(items));

            CollectionAssert.AreEqual(
                items.Select(i => (i.Message, i.Stack, i.Level)).ToArray(),
                restored.Select(i => (i.Message, i.Stack, i.Level)).ToArray());
        }

        [Test] public void Deserialize_EmptyOrGarbage_YieldsEmpty_NoThrow()
        {
            Assert.IsEmpty(LogCapture.Deserialize(""));
            Assert.IsEmpty(LogCapture.Deserialize(null));
            Assert.IsEmpty(LogCapture.Deserialize("not-json{"));
        }

        [Test] public void Serialize_ThenRestoreIntoBuffer_PreservesGetOutput()
        {
            var src = new LogBuffer(10);
            src.Add("a", "", LogLevel.Info);
            src.Add("b", "", LogLevel.Warning);

            var dst = new LogBuffer(10);
            foreach (var it in LogCapture.Deserialize(LogCapture.Serialize(src.Snapshot())))
            {
                dst.Add(it.Message, it.Stack, it.Level);
            }

            CollectionAssert.AreEqual(
                src.Get(null, 10).Select(i => i.Message).ToArray(),
                dst.Get(null, 10).Select(i => i.Message).ToArray());
        }
    }
}
