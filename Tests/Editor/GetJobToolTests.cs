using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Shtl.Mcp.Jobs;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    public class GetJobToolTests
    {
        // Изолированный ключ — не трогаем live-job работающего сервера (см. JobStore).
        const string Key = "Shtl.Mcp.Jobs.Test.GetJobToolTests";

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseString(Key);
        }

        [TearDown]
        public void TearDown()
        {
            SessionState.EraseString(Key);
        }

        [Test]
        public void UnknownId_ReturnsStructuredError()
        {
            var tool = new GetJobTool(new JobStore(Key));
            var o = tool.Invoke(new JObject { ["jobId"] = "nope" });
            StringAssert.Contains("unknown", (string)o["error"]);
        }

        [Test]
        public void MissingArg_ReturnsError()
        {
            var tool = new GetJobTool(new JobStore(Key));
            var o = tool.Invoke(new JObject());
            StringAssert.Contains("required", (string)o["error"]);
        }

        [Test]
        public void KnownJob_ReturnsStatusAndResult()
        {
            var store = new JobStore(Key);
            var id = store.Create("run_tests");
            store.Complete(id, "{\"passed\":5}");

            var o = new GetJobTool(store).Invoke(new JObject { ["jobId"] = id });

            Assert.AreEqual(id, (string)o["jobId"]);
            Assert.AreEqual("run_tests", (string)o["tool"]);
            Assert.AreEqual("done", (string)o["status"]);
            StringAssert.Contains("passed", (string)o["result"]);
        }

        [Test]
        public void NeedsMainThread_IsFalse()
        {
            Assert.IsFalse(new GetJobTool(new JobStore(Key)).NeedsMainThread);
        }
    }
}
