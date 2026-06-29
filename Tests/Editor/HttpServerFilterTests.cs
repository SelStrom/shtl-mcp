using NUnit.Framework;
using Shtl.Mcp.Server;

namespace Shtl.Mcp.Editor.Tests
{
    /// Регресс-защита Host/Origin-фильтра (DNS-rebinding / CSRF). Чистый IsRequestAllowed — без листенера.
    public class HttpServerFilterTests
    {
        const int Port = 9730;

        [Test] public void Loopback127_NoOrigin_Allowed()
        {
            Assert.IsTrue(HttpServer.IsRequestAllowed("127.0.0.1:" + Port, null, Port));
        }

        [Test] public void Localhost_NoOrigin_Allowed()
        {
            Assert.IsTrue(HttpServer.IsRequestAllowed("localhost:" + Port, null, Port));
        }

        [Test] public void EmptyOrigin_TreatedAsAbsent_Allowed()
        {
            Assert.IsTrue(HttpServer.IsRequestAllowed("127.0.0.1:" + Port, "", Port));
        }

        [Test] public void AnyOrigin_Rejected_CrossOriginBrowser()
        {
            Assert.IsFalse(HttpServer.IsRequestAllowed("127.0.0.1:" + Port, "http://evil.example", Port));
        }

        [Test] public void MissingHost_Rejected_FailClosed()
        {
            Assert.IsFalse(HttpServer.IsRequestAllowed(null, null, Port));
        }

        [Test] public void ForeignHost_Rejected_DnsRebinding()
        {
            Assert.IsFalse(HttpServer.IsRequestAllowed("evil.example:" + Port, null, Port));
        }

        [Test] public void WrongPort_Rejected()
        {
            Assert.IsFalse(HttpServer.IsRequestAllowed("127.0.0.1:1234", null, Port));
        }

        [Test] public void HostWithoutPort_Rejected()
        {
            Assert.IsFalse(HttpServer.IsRequestAllowed("127.0.0.1", null, Port));
        }
    }
}
