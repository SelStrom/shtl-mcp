using System;
using System.Threading;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Dispatch;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T1a bg-liveness (AC4.8): dispatcher штампует LastDrainUtc; ping отвечает на фоне (NeedsMainThread=false)
    /// и отличает «главный поток завис» от «сервер мёртв».
    public class PingToolTests
    {
        [Test]
        public void Dispatcher_Drain_UpdatesLastDrainUtc()
        {
            var d = new MainThreadDispatcher();
            var before = d.LastDrainUtc;
            Thread.Sleep(20);
            d.Drain();
            Assert.Greater(d.LastDrainUtc, before, "Drain должен обновить метку тика главного потока");
        }

        [Test]
        public void Ping_NeedsMainThread_IsFalse()
        {
            // ключевое: ping обязан отвечать с фонового потока, иначе он бесполезен при блоке главного
            Assert.IsFalse(new PingTool(() => DateTime.UtcNow, () => 0).NeedsMainThread);
        }

        [Test]
        public void Ping_RecentDrain_Responsive_NoNote()
        {
            var o = new PingTool(() => DateTime.UtcNow, () => 42.0).Invoke(new JObject());
            Assert.IsTrue((bool)o["alive"]);
            Assert.IsTrue((bool)o["mainThreadResponsive"]);
            Assert.AreEqual(42, (int)o["listenerUptimeSeconds"]);
            Assert.IsNull(o["note"]);
        }

        [Test]
        public void Ping_StaleDrain_Wedged_WithNote()
        {
            var o = new PingTool(() => DateTime.UtcNow.AddSeconds(-30), () => 10.0).Invoke(new JObject());
            Assert.IsFalse((bool)o["mainThreadResponsive"], "30с без дренажа → главный поток завис");
            Assert.GreaterOrEqual((double)o["mainThreadAgeSeconds"], 5.0);
            StringAssert.Contains("blocked", (string)o["note"]);
        }
    }
}
