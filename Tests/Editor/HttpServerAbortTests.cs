using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Shtl.Mcp.Server;

namespace Shtl.Mcp.Editor.Tests
{
    /// P3: Abort освобождает порт немедленно (без TIME_WAIT), чтобы после reload тот же порт биндился сразу.
    public class HttpServerAbortTests
    {
        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        [Test]
        public void Abort_ReleasesPort_ForImmediateRebind()
        {
            int port = FreePort();
            var a = new HttpServer(port, _ => "", null);
            a.Start();
            Assert.IsTrue(a.IsListening, "first bind");
            a.Abort();

            var b = new HttpServer(port, _ => "", null);
            b.Start();
            Assert.IsTrue(b.IsListening, "same port rebinds immediately after Abort");
            b.Abort();
        }
    }
}
