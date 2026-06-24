using NUnit.Framework;
using Shtl.Mcp.Common;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// T11: конфиг (EditorPrefs) round-trip + clamp + параметризуемый диапазон портов. `Enabled` НЕ трогаем —
    /// иначе watchdog остановил бы живой сервер. Остальные значения снимаются/возвращаются.
    public class ShtlMcpConfigTests
    {
        int _start, _count, _hb;
        bool _csharp;

        [SetUp]
        public void SetUp()
        {
            _start = ShtlMcpConfig.PortRangeStart;
            _count = ShtlMcpConfig.PortRangeCount;
            _hb = ShtlMcpConfig.HeartbeatSeconds;
            _csharp = ShtlMcpConfig.AllowRunCsharp;
        }

        [TearDown]
        public void TearDown()
        {
            ShtlMcpConfig.PortRangeStart = _start;
            ShtlMcpConfig.PortRangeCount = _count;
            ShtlMcpConfig.HeartbeatSeconds = _hb;
            ShtlMcpConfig.AllowRunCsharp = _csharp;
        }

        [Test]
        public void RoundTrip_PortRange_And_RunCsharpFlag()
        {
            ShtlMcpConfig.PortRangeStart = 8123;
            ShtlMcpConfig.PortRangeCount = 42;
            ShtlMcpConfig.AllowRunCsharp = true;

            Assert.AreEqual(8123, ShtlMcpConfig.PortRangeStart);
            Assert.AreEqual(42, ShtlMcpConfig.PortRangeCount);
            Assert.IsTrue(ShtlMcpConfig.AllowRunCsharp);
        }

        [Test]
        public void Heartbeat_ClampedToMin1()
        {
            ShtlMcpConfig.HeartbeatSeconds = 0;
            Assert.AreEqual(1, ShtlMcpConfig.HeartbeatSeconds, "период тика не может быть < 1с");
        }

        [Test]
        public void PortAllocator_CustomRange_StaysInRange()
        {
            int port = PortAllocator.Allocate("some/project/path", _ => true, 8000, 50);
            Assert.GreaterOrEqual(port, 8000);
            Assert.Less(port, 8050);
        }
    }
}
