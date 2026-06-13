using NUnit.Framework;
using Shtl.Mcp.Common;

namespace Shtl.Mcp.Editor.Tests
{
    public class ServerNameTests
    {
        [Test] public void Base_WhenNoCollision()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/p", _ => null));

        [Test] public void Base_WhenSamePathAlreadyLive()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/p", _ => "/p"));

        [Test] public void Suffixed_WhenNameCollidesWithDifferentPath()
        {
            string name = ServerName.Resolve("PerfectWar", "/worktree", _ => "/main");
            StringAssert.StartsWith("unity-perfectwar-", name);
            Assert.AreEqual("unity-perfectwar-" + Fnv.Hash4("/worktree"), name);
        }

        [Test] public void Sanitize_LowercasesAndDashes()
            => Assert.AreEqual("unity-my-game", ServerName.Resolve("My Game!", "/p", _ => null));
    }
}
