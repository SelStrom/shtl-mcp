using NUnit.Framework;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// Адаптивный keepalive: full-rate только при включённом сервере+тогле И недавнем запросе клиента.
    public class KeepAlivePolicyTests
    {
        [TestCase(true, true, 5.0, true, TestName = "active_client_on")]
        [TestCase(true, true, 300.0, false, TestName = "idle_client_off")]
        [TestCase(true, true, -1.0, false, TestName = "no_client_off")]
        [TestCase(true, false, 5.0, false, TestName = "toggle_off")]
        [TestCase(false, true, 5.0, false, TestName = "server_off")]
        public void WantKeepAlive_Cases(bool en, bool tog, double age, bool exp)
        {
            Assert.AreEqual(exp, ShtlMcpServer.WantKeepAlive(en, tog, age));
        }
    }
}
