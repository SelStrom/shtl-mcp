using NUnit.Framework;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// Ре-регистрация --scope user нужна только при смене порта против ранее зарегистрированного.
    public class PortReRegisterTests
    {
        [TestCase(0, 9750, false, TestName = "first_time_no_prior")]
        [TestCase(9750, 9750, false, TestName = "same_port_noop")]
        [TestCase(9750, 9751, true, TestName = "port_drifted_rereg")]
        public void ShouldReRegister_Cases(int last, int cur, bool exp)
        {
            Assert.AreEqual(exp, ShtlMcpServer.ShouldReRegister(last, cur));
        }
    }
}
